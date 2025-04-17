using Google.Apis.Auth.OAuth2.Flows;
using Google.Apis.Auth.OAuth2;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using ShopifyService = GoogleLogin.Services.ShopifyService;
using GoogleLogin.Services;
using GoogleLogin.Models;
using System.Data.Entity;
using System.Threading.Tasks;

namespace GoogleLogin.Controllers
{
    [Authorize]
    public class SettingController : Controller
    {
        private readonly IServiceScopeFactory       _serviceScopeFactory;
        private readonly ILogger<HomeController>    _logger;
        private SignInManager<AppUser>              _signInManager;
        private UserManager<AppUser>                _userManager;
        private readonly EMailService               _emailService;
        private readonly EMailTokenService          _emailTokenService;
        private readonly IConfiguration             _configuration;
        public static readonly string[] Scopes = { "email", "profile", "https://www.googleapis.com/auth/gmail.modify" };

        public SettingController(
            SignInManager<AppUser>  signinMgr,
            UserManager<AppUser>    userMgr,
            IServiceScopeFactory    serviceScopeFactory,
            EMailService            emailService,
            EMailTokenService       emailTokenService,
            ILogger<HomeController> logger,
            IConfiguration          configuration)
        {
            _serviceScopeFactory    = serviceScopeFactory;
            _signInManager          = signinMgr;
            _userManager            = userMgr;
            _logger                 = logger;
            _emailService           = emailService;
            _emailTokenService      = emailTokenService;
            _configuration          = configuration;
        }

        [HttpGet]
        public IActionResult Index()
        {
            return View();
        }

        [HttpGet]
        public IActionResult MailManage()
        {
            ViewBag.menu = "setting";
            ViewBag.subMenu = "subMenu";
            return View("View_MailManage");
        }

        [HttpPost]
        public async Task<IActionResult> UpdateMail(string strMailIdx)
        {
            if (string.IsNullOrWhiteSpace(strMailIdx))
            {
                return Json(new { status = -201, message = "Invalid mail index" });
            }

            using (var scope = _serviceScopeFactory.CreateScope())  // Create a new scope
            {
                var _dbContext = scope.ServiceProvider.GetRequiredService<AppIdentityDbContext>();

                if (!int.TryParse(strMailIdx, out int mailIdx))
                {
                    return Json(new { status = -201, message = "Mail index must be a valid number" });
                }

                bool bRes = await _emailTokenService.RefreshTokenAync(mailIdx);

                if ( bRes ) {
                    return Json(new { status = 201, message = "Record update successfully" });
                }

                return Json(new { status = -201, message = "Record update failed" });
            }
        }

        [HttpPost]
        public IActionResult DeleteMail(string strMailIdx)
        {
            if (string.IsNullOrWhiteSpace(strMailIdx))
            {
                return Json(new { status = -201, message = "Invalid mail index" });
            }

            using (var scope = _serviceScopeFactory.CreateScope())  // Create a new scope
            {
                var _dbContext = scope.ServiceProvider.GetRequiredService<AppIdentityDbContext>();

                if (!int.TryParse(strMailIdx, out int mailIdx))
                {
                    return Json(new { status = -201, message = "Mail index must be a valid number" });
                }

                var pMailAccount = _dbContext.TbMailAccount.FirstOrDefault(e => e.id == mailIdx);

                if (pMailAccount == null)
                {
                    return Json(new { status = -201, message = "Record not found" });
                }

                _dbContext.TbMailAccount.Remove(pMailAccount);
                _dbContext.SaveChanges();

                return Json(new { status = 201, message = "Record deleted successfully" });
            }
        }

        [HttpPost]
        public IActionResult GetMailList()
        {
            using (var scope = _serviceScopeFactory.CreateScope())  // Create a new scope
            {
                var _dbContext = scope.ServiceProvider.GetRequiredService<AppIdentityDbContext>();
                List<TbMailAccount> mailList = _dbContext.TbMailAccount.Where(e => e.mail != "" && e.userId == _userManager.GetUserId(HttpContext.User)).ToList();

                ViewBag.mailList = mailList;
                return PartialView("View_MailList");
            }
        }

        [HttpPost]
        public IActionResult RegisterNewMail()
        {
            var flow = new GoogleAuthorizationCodeFlow(
                new GoogleAuthorizationCodeFlow.Initializer
                {
                    ClientSecrets = new ClientSecrets
                    {
                        ClientId = _configuration["clientId"],
                        ClientSecret = _configuration["clientSecret"]
                    },
                    Scopes = Scopes,
                    Prompt = "select_account consent",
                });

            var request = HttpContext.Request;
            var hostUrl = $"{request.Scheme}://{request.Host}{request.PathBase}";
            string redirectUri = $"{hostUrl}/OAuth2Callback";

            string authorizationUrl = flow.CreateAuthorizationCodeRequest(redirectUri).Build().ToString();
            HttpContext.Session.SetString("RedirectUri", $"{hostUrl}/setting/mailmanage");
            return Json(new { status = 201, authorizationUrl = authorizationUrl });
        }

        [HttpGet]
        public IActionResult ShopifyManage()
        {
            ViewBag.menu = "setting";
            ViewBag.subMenu = "shopify";
            return View("View_ShopifyManage");
        }

        [HttpPost]
        public IActionResult GetShopifyList()
        {
            using (var scope = _serviceScopeFactory.CreateScope())  // Create a new scope
            {
                var _dbContext = scope.ServiceProvider.GetRequiredService<AppIdentityDbContext>();
                string strUserId = _userManager.GetUserId(HttpContext.User) ?? "";
                List<TbShopifyToken> shopifyList =
                    _dbContext.TbTokens.Where(e => e.UserId == strUserId)
                                .ToList();

                ViewBag.shopifyList = shopifyList;
                return PartialView("View_shopifyList");
            }
        }

        [HttpPost]
        public IActionResult DeleteShopify(string strShopifyIdx)
        {
            if (string.IsNullOrWhiteSpace(strShopifyIdx))
            {
                return Json(new { status = -201, message = "Invalid shopify index" });
            }

            using (var scope = _serviceScopeFactory.CreateScope())  // Create a new scope
            {
                var _dbContext = scope.ServiceProvider.GetRequiredService<AppIdentityDbContext>();

                if (!int.TryParse(strShopifyIdx, out int shopifyIdx))
                {
                    return Json(new { status = -201, message = "Shopify index must be a valid number" });
                }

                var pShopify = _dbContext.TbTokens.FirstOrDefault(e => e.idx == shopifyIdx);

                if (pShopify == null)
                {
                    return Json(new { status = -201, message = "Record not found" });
                }

                _dbContext.TbTokens.Remove(pShopify);
                _dbContext.SaveChanges();

                return Json(new { status = 201, message = "Record deleted successfully" });
            }
        }

        [HttpGet]
        public IActionResult TwilioManage()
        {
            string userId = _userManager.GetUserId(HttpContext.User) ?? string.Empty;

            using (var scope = _serviceScopeFactory.CreateScope())
            {
                var _dbContext = scope.ServiceProvider.GetRequiredService<AppIdentityDbContext>();
                try
                {
                    var _one = _dbContext.TbTwilios.Where(e => e.userid == userId).FirstOrDefault();

                    if (_one == null)
                    {
                        _one = new TbTwilio();
                    }

                    ViewBag.twilioAccount = _one;
                } catch (Exception ex)
                {
                    Console.WriteLine(ex.StackTrace);
                }
            }
            ViewBag.menu = "setting";
            ViewBag.subMenu = "twilio";
            return View("View_TwilioManage");
        }

        [HttpGet]
        public IActionResult BlandManage()
        {
            ViewBag.menu = "setting";
            ViewBag.subMenu = "bland";
            return View("View_BlandManage");
        }

        [HttpPost] 
        public IActionResult saveTwilio(TwilioSaveModel model)
        {
            if (  ModelState.IsValid )
            {
                string userId = _userManager.GetUserId(HttpContext.User) ?? string.Empty;

                if (string.IsNullOrEmpty(userId))
                {
                    return Json(new { status = -201, message = "Save failed!" });
                }

                using (var scope = _serviceScopeFactory.CreateScope())
                {
                    var _dbContext = scope.ServiceProvider.GetRequiredService<AppIdentityDbContext>();

                    try
                    {
                        var _one = _dbContext
                                    .TbTwilios
                                    .Where(e => e.userid == userId)
                                    .FirstOrDefault();

                        if (_one == null)
                        {
                            _dbContext.Add(new TbTwilio
                            {
                                userid = userId,
                                accountsid = model.accountsid ?? string.Empty,
                                authtoken = model.authtoken ?? string.Empty,
                                phonenumber = model.phonenumber ?? string.Empty,
                            }); ;
                        }
                        else
                        {
                            _one.accountsid = model.accountsid ?? string.Empty;
                            _one.authtoken = model.authtoken ?? string.Empty;
                            _one.phonenumber = model.phonenumber ?? string.Empty;
                        }

                        _dbContext.SaveChanges();
                        return Json(new { status = 201, message = "Saved successfully!" });
                    } catch (Exception e)
                    {
                        Console.WriteLine(e.StackTrace);
                    }
                }
            } 
            return Json(new { status = -201, message = "Save failed!" });
        }
    }
}