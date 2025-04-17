using GoogleLogin.Models;
using GoogleLogin.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using System.Security.Cryptography;
using System.Text;
using ShopifyService = GoogleLogin.Services.ShopifyService;

namespace GoogleLogin.Controllers
{
	public class ShopifyController : Controller
	{
		private readonly string                             _clientId;
		private readonly string                             _clientSecret;
        private readonly string                             _installlink;
		private readonly string                             _domain;
        private readonly UserManager<AppUser>               _userManager;
		private readonly ShopifyService                     _shopifyService;
        private readonly EMailService                       _emailService;
        private readonly ILogger<ShopifyController>         _logger;
        private const int PerPageCnt = 10;

        public ShopifyController(
            UserManager<AppUser>        userMgr, 
            ShopifyService              shopifyService, 
            EMailService                emailService,
            IConfiguration              configuration, 
            ILogger<ShopifyController>  logger)
        {
            _clientId               = configuration["Shopify:clientId"]  ?? "";
            _clientSecret           = configuration["Shopify:ApiSecret"] ?? "";
            _installlink            = configuration["Shopify:installlink"] ?? "";
            _domain                 = configuration["Domain"] ?? "";
            _userManager            = userMgr;
            _shopifyService         = shopifyService;
            _emailService           = emailService;
            _logger                 = logger;
        }

		[HttpGet]
		public async Task<IActionResult> Orders(string store, int nPageNo = 0)
		{
            var user = await _userManager.GetUserAsync(User);

            if (user == null)
            {
#if DEBUG
                user = new AppUser();
				user.Email = "sherman@zahavas.com";
#else
                return RedirectToAction("Login");
#endif
            }

            ViewBag.scripts = new List<string>(){"/js/orders.js"};
            ViewBag.Store = store;
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> Order(string store, int nPageNo = 0)
        {
            var user = await _userManager.GetUserAsync(User);

            if (user == null)
            {
#if DEBUG
                user = new AppUser();
                user.Email = "sherman@zahavas.com";
#else
                return PartialView("Order");
#endif
            }

            if (nPageNo <= 0)
            {
                nPageNo = 1;
            }
            int nRecCnt = await _shopifyService.GetOrdersPerPageCnt(store, nPageNo, PerPageCnt);
            int nPageCnt = nRecCnt / PerPageCnt + 1;
            if (nPageNo >= nPageCnt)
            {
                nPageNo = nPageCnt;
            }

            List<TbOrder> lstOrders = await _shopifyService.GetOrders(store, nPageNo, PerPageCnt);

            ViewBag.orders      = lstOrders;
            ViewBag.PageNo      = nPageNo;
            ViewBag.AllCnt      = nRecCnt;
            ViewBag.PageCnt     = nPageCnt;
            ViewBag.PagePerCnt  = PerPageCnt;
            ViewBag.Store       = store;

            return PartialView("Order");
        }

        [HttpPost("shopify/auth")]
        public IActionResult Authenticate(string shop)
        {
            ShopifyAuthHelper pHelper = new ShopifyAuthHelper(_clientId, _clientSecret);
            if (string.IsNullOrEmpty(shop))
            {
                shop = "punkcaseca.myshopify.com";
            }
           
            var authUrl = _installlink;
            return Json(new { status = 201, authorizationUrl = authUrl });
        }

        [HttpGet("shopify/callback")]
        public async Task<IActionResult> Callback(string shop, string code, string hmac)
        {
            var authHelper = new ShopifyAuthHelper(_clientId, _clientSecret);
            var accessToken = await authHelper.ExchangeCodeForAccessToken(shop, code);

            string userId = _userManager.GetUserId(HttpContext.User) ?? "";
           _logger.LogInformation("Shopify callbacked called before Save");
            await _shopifyService.SaveAccessToken(userId, shop, accessToken);
            await _shopifyService.RegisterHookEntry(shop, accessToken);
             _logger.LogInformation("Shopify callbacked called after Save");
            return RedirectToAction("shopifymanage", "setting");
        }

        private bool VerifyHmac(string hmac, IQueryCollection query, string sharedSecret)
        {
            // Extract query parameters except 'hmac'
            var sortedParams = query
                .Where(kvp => kvp.Key != "hmac")
                .OrderBy(kvp => kvp.Key)
                .Select(kvp => $"{kvp.Key}={kvp.Value}");

            var data = string.Join("&", sortedParams);

            using (var hmacsha256 = new HMACSHA256(Encoding.UTF8.GetBytes(sharedSecret)))
            {
                var hash = hmacsha256.ComputeHash(Encoding.UTF8.GetBytes(data));
                var computedHmac = BitConverter.ToString(hash).Replace("-", "").ToLower();

                return hmac == computedHmac;
            }
        }

        [HttpGet("shopify/install")]
        public IActionResult Install(string host, string hmac, string shop, string state)
        {
            if (!VerifyHmac(hmac, Request.Query, _clientSecret))
            {
                return BadRequest("Invalid HMAC signature");
            }
            try
            {
                string decodeHost = DecodeHost(host);
            
                if (string.IsNullOrEmpty(decodeHost) || string.IsNullOrEmpty(shop))
                {
                    return BadRequest("Required parameters missing");
                }
                var authHelper = new ShopifyAuthHelper(_clientId, _clientSecret);
                string strRedirectUrl = authHelper.BuildAuthorizationUrl(decodeHost, $"{_domain}shopify/callback");
                return Redirect(strRedirectUrl);
            }
            catch(Exception ex)
            {
                Console.WriteLine("shopify/install" + ex.Message);
            }

            return BadRequest("Failed to retrieve access token");
        }

        private string DecodeHost(string base64Host)
        {
            try
            {
                // Add padding if necessary
                if (base64Host.Length % 4 != 0)
                {
                    base64Host = base64Host.PadRight(base64Host.Length + (4 - base64Host.Length % 4), '=');
                }

                byte[] data = Convert.FromBase64String(base64Host);
                return Encoding.UTF8.GetString(data);
            }
            catch (FormatException ex)
            {
                Console.WriteLine($"Error decoding Base64 host: {ex.Message}");
                return string.Empty;
            }
        }

        
        [HttpGet("shopify/orderrefresh")]
        public async Task<IActionResult> RefreshOrder(string strStore)
        {
            string strToken = await _shopifyService.GetAccessTokenByStore(strStore);
            if(string.IsNullOrEmpty(strToken))
            {
                return await Order(strStore);
            }

            await _shopifyService.OrderRequest(strStore, strToken);
            return await Order(strStore);
        }

        /********************************webhook****************************************/
        [HttpPost("shopify/order_create")]
        public async Task<IActionResult> OrderCreate()
        {
            try
            {
                string requestBody;
                using (var reader = new StreamReader(Request.Body))
                {
                    requestBody = await reader.ReadToEndAsync();
                }

                string hmacHeader = Request.Headers["X-Shopify-Hmac-Sha256"].FirstOrDefault() ?? string.Empty;

                if (!VerifyWebhook(requestBody, hmacHeader, _clientSecret))
                {
                    return Unauthorized("Invalid HMAC signature");
                }

                _logger.LogInformation($"Webhook create request 1: {requestBody}");
                await _shopifyService.SaveNewOrder(requestBody);
                _logger.LogInformation($"Webhook create request 2: {requestBody}");
                return Ok();
            }
            catch (Exception ex)
            {
                // Log or handle errors
                _logger.LogInformation($"Error processing webhook: {ex.Message}");
                return StatusCode(500, "Internal Server Error");
            }
        }

        [HttpPost("shopify/order_cancelled")]
        public async Task<IActionResult> OrderCancelled()
        {
            try
            {
                string requestBody;
                using (var reader = new StreamReader(Request.Body))
                {
                    requestBody = await reader.ReadToEndAsync();
                }

                string hmacHeader = Request.Headers["X-Shopify-Hmac-Sha256"].FirstOrDefault() ?? string.Empty;

                if (!VerifyWebhook(requestBody, hmacHeader, _clientSecret))
                {
                    return Unauthorized("Invalid HMAC signature");
                }

                _logger.LogInformation($"Webhook cancelled request : {requestBody}");
                await _shopifyService.SaveNewOrder(requestBody);
                return Ok();
            }
            catch (Exception ex)
            {
                // Log or handle errors
                _logger.LogError($"Error processing webhook: {ex.Message}");
                return StatusCode(500, "Internal Server Error");
            }
        }

        private bool VerifyWebhook(string requestBody, string hmacHeader, string sharedSecret)
        {
            var encoding = new System.Text.UTF8Encoding();
            var key = encoding.GetBytes(sharedSecret);

            using (var hmac = new System.Security.Cryptography.HMACSHA256(key))
            {
                var hash = hmac.ComputeHash(encoding.GetBytes(requestBody));
                var calculatedHmac = Convert.ToBase64String(hash);

                return calculatedHmac == hmacHeader;
            }
        }
    }
}
