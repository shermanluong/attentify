
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using GoogleLogin.Services;
using GoogleLogin.Models;
using Stripe.Checkout;
using Stripe;
using WebSocketSharp;

namespace GoogleLogin.Controllers
{
    [Authorize]
    public class StripeController : Controller
    {
        private readonly IServiceScopeFactory       _serviceScopeFactory;
        private readonly ILogger<HomeController>    _logger;
        private SignInManager<AppUser>              _signInManager;
        private UserManager<AppUser>                _userManager;
        private readonly EMailService               _emailService;
        private readonly IConfiguration             _configuration;
        private readonly StripeService              _stripeService;
        private readonly string                     _publishKey;
        private readonly string                     _secretKey;

        public StripeController(
            SignInManager<AppUser>      signinMgr,
            UserManager<AppUser>        userMgr,
            IServiceScopeFactory        serviceScopeFactory,
            EMailService                emailService,
            ILogger<HomeController>     logger,
            IConfiguration              configuration,
            StripeService               stripeService)
        {
            _serviceScopeFactory    = serviceScopeFactory;
            _signInManager          = signinMgr;
            _userManager            = userMgr;
            _logger                 = logger;
            _emailService           = emailService;
            _configuration          = configuration;
            _stripeService          = stripeService;
            _publishKey             = _configuration["Stripe:publishKey"] ?? "";
            _secretKey              = _configuration["Stripe:secretKey"] ?? "";
        }

        public IActionResult index()
        {
            ViewBag.menu = "setting";
            ViewBag.subMenu = "payment";
            return View("View_payment");
        }

        [HttpPost("/createCheckoutSession")]
        public IActionResult createCheckoutSession(int planType)
        {
            StripeConfiguration.ApiKey = _secretKey;

            var plan = _stripeService.getPlanById(planType);

            var domain = _configuration["Domain"];

            var options = new SessionCreateOptions
            {
                PaymentMethodTypes = new List<string> { "card" },
                Mode = "subscription",
                LineItems = new List<SessionLineItemOptions>
                {
                    new SessionLineItemOptions
                    {
                        Price = plan?.priceId,
                        Quantity = 1,
                    },
                },
                SuccessUrl = $"{domain}stripe/success?session_id={{CHECKOUT_SESSION_ID}}",
                CancelUrl = $"{domain}stripe/cancel",
            };

            var service = new SessionService();
            Session session = service.Create(options);

            return Json(new { url = session.Url });
        }

        public async Task<IActionResult> success()
        {
            var user = await _userManager.GetUserAsync(HttpContext.User);
            string userEmail = user?.Email ?? "";

            ViewBag.menu = "setting";
            ViewBag.subMenu = "payment";

            var userPlan = _stripeService.getUserPlanDetail(userEmail);
            if (userPlan != null )
            {
                ViewBag.planName = userPlan.planName;
                ViewBag.expireDate = DateTimeOffset.FromUnixTimeSeconds(userPlan.expire).DateTime;
            }
           
            return View("View_Success"); 
        }

        public IActionResult cancel()
        {
            ViewBag.menu = "setting";
            ViewBag.subMenu = "payment";
            return View("View_Error"); 
        }

        [AllowAnonymous]
        public async Task<IActionResult> webhook()
        {
            var json = await new StreamReader(HttpContext.Request.Body).ReadToEndAsync();
            var endpointSecret = _configuration["stripe:webhookSecret"];
            Console.WriteLine("OK");
            try
            {
                var stripeEvent = EventUtility.ConstructEvent(json, Request.Headers["Stripe-Signature"], endpointSecret);

                if ( stripeEvent.Type == "checkout.session.completed" )
                {
                    var session = stripeEvent.Data.Object as Session;

                    if (session != null)
                    {
                        var customerEmail = session.CustomerDetails.Email;
                        var subscriptionId = session.SubscriptionId;

                        var subscriptionService = new SubscriptionService();
                        var subscription = await subscriptionService.GetAsync(subscriptionId);

                        var priceId = subscription.Items.Data.FirstOrDefault()?.Price.Id ?? string.Empty;
                        var currentPeriodEnd = subscription.CurrentPeriodEnd;

                        var _one = _stripeService.getPlanByPriceId(priceId);

                        if (_one == null) return Ok();

                        _stripeService.addUserPlan(customerEmail, _one.id, new DateTimeOffset(currentPeriodEnd).ToUnixTimeSeconds());

                        Console.WriteLine($"Subscription started for: {customerEmail}");
                        Console.WriteLine($"Plan ID: {priceId}");
                        Console.WriteLine($"Subscription Expiration Date: {currentPeriodEnd}");
                    }
                    else
                    {
                        Console.WriteLine("Failed to cast session object.");
                    }
                }

                return Ok();
            }
            catch (StripeException e)
            {
                Console.WriteLine($"Stripe Webhook Error: {e.Message}");
                return BadRequest();
            }
        }
    }
}