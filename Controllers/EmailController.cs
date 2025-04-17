using Google.Apis.Auth.OAuth2.Flows;
using Google.Apis.Auth.OAuth2;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json.Linq;
using System.Web;
using ShopifyService = GoogleLogin.Services.ShopifyService;
using GoogleLogin.Models;
using GoogleLogin.Services;
using WebSocketSharp;
using Microsoft.CodeAnalysis.Elfie.Serialization;
using System.Text.Json;
using System.Linq.Expressions;
using MailKit;

namespace GoogleLogin.Controllers
{
    [Authorize]
    public class EmailController : Controller
    {
        private readonly ILogger<EmailController>   _logger;
        private SignInManager<AppUser>              _signInManager;
        private UserManager<AppUser>                _userManager;
        private readonly EMailService               _emailService;
        private readonly EMailTokenService          _emailTokenService;
        private readonly ShopifyService             _shopifyService;
        private readonly SmsService                 _smsService;
        private readonly LLMService                 _llmService;
        private readonly IConfiguration             _configuration;
        private readonly IServiceScopeFactory       _serviceScopeFactory;
        private readonly string                     _phoneNumber;
        public static readonly string[]             Scopes = { "email", "profile", "https://www.googleapis.com/auth/gmail.modify" };
        private const int           nCntPerPage = 20;

        public EmailController(
            SignInManager<AppUser>  signinMgr, 
            IServiceScopeFactory    serviceScopeFactory,
            UserManager<AppUser>    userMgr, 
            EMailService            service, 
            EMailTokenService       emailTokenService,
            ShopifyService          shopifyService,
            SmsService              smsService, 
            ILogger<EmailController> logger, 
            IConfiguration          configuration, 
            LLMService              llmService)
        {
            _signInManager          = signinMgr;
            _serviceScopeFactory    = serviceScopeFactory;
            _userManager            = userMgr;
            _emailService           = service;
            _emailTokenService      = emailTokenService;
            _shopifyService         = shopifyService;
            _logger                 = logger;
            _smsService             = smsService;
            _configuration          = configuration;
            _phoneNumber            = _configuration["Twilio:PhoneNumber"] ?? "";
            _llmService             = llmService;
        }

        [HttpGet]
        public async Task<IActionResult> Index()
        {
            AppUser? user = await _userManager.GetUserAsync(HttpContext.User);

            ViewBag.menu = "email";
            ViewBag.mailAccountList = _emailTokenService.GetMailAccountList(_userManager.GetUserId(HttpContext.User) ?? "");
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> GetMailList(string strEmail, int nPageIndex = 0, int nEmailState = 0)
        {
            if (strEmail != "All")
            {
                string accessToken = _emailTokenService.GetAccessTokenFromMailName(strEmail);

                bool bTokenExpired = await _emailTokenService.IsAccessTokenExpired(accessToken);

                if ( !bTokenExpired ) {
                    _logger.LogInformation("Gmail Token is available.");
                    _emailService.UpdateMailDatabaseAsync(accessToken, strEmail, 10);
                } else {
                    _logger.LogWarning("Gmail Token is expired.");
                    AppUser? user = await _userManager.GetUserAsync(HttpContext.User);
                    string userId = user?.Id ?? string.Empty;
                    bool bRes = await _emailTokenService.RefreshTokenAync(strEmail, userId);

                    if ( bRes ) {
                        _logger.LogInformation("Gmail Token has updated.");
                    } else {
                        _logger.LogWarning("Gmail Token updating has failed.");
                    }
                }
            }

            int nMailCnt = _emailService.GetMailCnt(strEmail, nEmailState);
            var emailList = _emailService.GetMailList(strEmail, nPageIndex, nCntPerPage, nEmailState);

            List<TbEmailsExt> emailExtList = new List<TbEmailsExt>();
            foreach (var email in emailList)
            {
                emailExtList.Add(new TbEmailsExt(email));
            }

            ViewBag.Emails          = emailExtList;
            ViewBag.nMailTotalCnt   = nMailCnt;

            return PartialView("View_EmailList");
        }

        [HttpGet("email/detail")]
        public async Task<IActionResult> EmailDetail(string id, string strToEmail)
        {
            if (id == string.Empty) return BadRequest("Mail Id must be required!");

            EmailExt emailExt       = _emailService.GetMailDetail(id);
            List<EmailExt> listResult = new List<EmailExt>();
            listResult = _emailService.GetMailDetailList(emailExt.em_from, emailExt.em_to);

            ViewBag.customerInfo    = _emailService.GetCustomerInfo(id);
            ViewBag.emailExt        = emailExt;
            ViewBag.emailList       = listResult;
            ViewBag.strToEmail      = strToEmail;
            
            string strRespond = string.Empty;
            string strMailEncodeBody = _emailService.GetMailEncodeBody(id);

            foreach (var item in listResult)
            {
                strMailEncodeBody += _emailService.GetMailEncodeBody(item.em_id);
            }
            //strMailEncodeBody = "";
            if (strMailEncodeBody != "")
            {
                strRespond = await _llmService.GetResponseLLM(strMailEncodeBody);
                _logger.LogInformation(strRespond);
                if (strRespond != string.Empty)
                {
                    JObject jsonObj = JObject.Parse(strRespond);
                    int    status = Convert.ToInt32((jsonObj["status"] ?? '0').ToString());
                    string type = (jsonObj["type"] ?? ' ').ToString();
                    string order_id = (jsonObj["order_id"] ?? ' ').ToString();
                    string msg = (jsonObj["msg"] ?? ' ').ToString();

                    if ( status == 0 ) {
                        ViewBag.replyMsg = msg; 
                    } 
                    if (status == 1)
                    {
                        _logger.LogInformation($"Order type is {type}");
                        string strOrderName = (jsonObj["order_id"] ?? "").ToString();
                        TbOrder tbOrder = _shopifyService.GetOrderInfo(strOrderName);
                        _logger.LogInformation($" logged 1 step");
                        if (tbOrder != null)
                        {
                            _logger.LogInformation($" logged 2 step");
                            _logger.LogInformation($"Order type is {type}");
                            if (type == "refund") {
                                ViewBag.replyMsg = $"Order(Id: {order_id}) is refunded.";
                            } else if ( type == "cancel" )
                            {
                                ViewBag.replyMsg = $"Order(Id: {order_id} is canceled)";
                            }  else {
                                ViewBag.replyMsg = $"What do you want with your order(Id: {order_id})";
                            }  

                            try
                            {
                                _logger.LogInformation($"Oder id from tbOrder {tbOrder.or_id}");
                                string orderDetail = await _shopifyService.GetOrderInfoRequest(tbOrder.or_id);
                                _logger.LogInformation("Oder detail ....");
                                _logger.LogInformation(orderDetail);
                                var jsonOrder = JsonDocument.Parse(orderDetail).RootElement.GetProperty("order");
                                var jsonCustomer = jsonOrder.GetProperty("customer");
                                var jsonAddress = jsonCustomer.GetProperty("default_address");

                                ViewBag.orderId = jsonOrder.GetProperty("id");
                                ViewBag.orderName = jsonOrder.GetProperty("name");
                                ViewBag.financial_status = jsonOrder.GetProperty("financial_status");
                                ViewBag.fulfillment_status = jsonOrder.GetProperty("fulfillment_status");
                                ViewBag.closed_at = jsonOrder.GetProperty("closed_at");
                                ViewBag.lineName = jsonOrder.GetProperty("line_items")[0].GetProperty("name");
                                ViewBag.deliveryMethod = jsonOrder.GetProperty("shipping_lines")[0].GetProperty("code");
                                ViewBag.sku = jsonOrder.GetProperty("line_items")[0].GetProperty("sku");
                                ViewBag.total_line_items_price = jsonOrder.GetProperty("total_line_items_price");
                                ViewBag.total_shipping_price_amount =
                                                jsonOrder.GetProperty("total_shipping_price_set")
                                                        .GetProperty("shop_money")
                                                        .GetProperty("amount");
                                ViewBag.current_total_price = jsonOrder.GetProperty("current_total_price");
                                ViewBag.current_total_discounts = jsonOrder.GetProperty("current_total_discounts");
                                _logger.LogInformation($"order_id is {ViewBag.orderId}");
                                Customer orderCustomerInfo = new Customer
                                {
                                    FirstName = jsonCustomer.GetProperty("first_name").ToString(),
                                    LastName = jsonCustomer.GetProperty("last_name").ToString(),
                                    Email = jsonCustomer.GetProperty("email").ToString(),
                                    Phone = jsonCustomer.GetProperty("phone").ToString(),
                                    shippingAddress = jsonAddress.GetProperty("name").ToString()
                                                    + jsonAddress.GetProperty("address1").ToString()
                                                    + jsonAddress.GetProperty("city").ToString()
                                                    + jsonAddress.GetProperty("zip").ToString()
                                };

                                ViewBag.orderCustomerInfo = orderCustomerInfo;
                            } catch(Exception ex)
                            {
                                _logger.LogInformation(ex.Message);
                            }
                            
                        }
                        else {
                            ViewBag.replyMsg = $"Order(Id: {order_id}) is invalid. Please give us a valid order Id.";
                        }
                    }
                }
            }
           
            return View("View_EmailDetail");
        }

        [HttpPost]
        public IActionResult GetMailCntInfo(string strEmail)
        {
            int nInboxCnt     = _emailService.GetMailCnt(strEmail, 0);
            int nArchievedCnt = _emailService.GetMailCnt(strEmail, 3);
            return Json(new { status = 200, nInboxCnt = nInboxCnt, nArchievedCnt = nArchievedCnt, nCntPerPage = nCntPerPage });
        }

        [HttpPost]
        public IActionResult GetCustomerInfo(string strMailId)
        {

            CustomerInfo customerInfo = _emailService.GetCustomerInfo(strMailId);
            return Json(new { status = 201, customerInfo = customerInfo });
        }

        [HttpPost]
        public async Task<IActionResult> GetOrderInfo(string strMailId)
        {
            try
            {
                var user = await _userManager.GetUserAsync(User);

                int status = 0;
                string strRespond = string.Empty;

                string strMailEncodeBody = _emailService.GetMailEncodeBody(strMailId);
                if (strMailEncodeBody != "")
                {
                    strRespond = await _llmService.GetResponseLLM(strMailEncodeBody);
                    JObject jsonObj = JObject.Parse(strRespond);
                    status = Convert.ToInt32((jsonObj["status"] ?? '0').ToString());
                }
                Console.WriteLine(strRespond);
                if (status == 1)
                {
                    JObject jsonObj = JObject.Parse(strRespond);
                    string strType = (jsonObj["type"] ?? "").ToString();
                    string strOrderName = (jsonObj["order_id"] ?? "").ToString();

                    TbOrder tbOrder = _shopifyService.GetOrderInfo(strOrderName);
                    Console.WriteLine(tbOrder);
                    if ( tbOrder != null)
                    {
                        string orderDetail = await _shopifyService.GetOrderInfoRequest(tbOrder.or_id);
                        ViewBag.status = 201;
                        ViewBag.orderName = strOrderName;
                        ViewBag.order = tbOrder;
                        ViewBag.orderDetail = orderDetail;
                        return PartialView("View_OrderDetail");
                    }
                } 
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }
            ViewBag.status = -201;
            return PartialView("View_OrderDetail");
        }

        [HttpPost("email/sendEmail")]
		public async Task<IActionResult> SendEmailAsync(string strTo, string strFrom, string strBody)
		{
			try
			{
				string accessToken = _emailTokenService.GetAccessTokenFromMailName(strFrom);
                _logger.LogInformation("strTo : " + strTo);
                _logger.LogInformation("strFrom : " + strFrom);
                _logger.LogInformation("strBody : " + strBody);
                _logger.LogInformation($"access tokoken is {accessToken}");
                bool isResult = false;

                if (!string.IsNullOrEmpty(accessToken)) {
                    isResult = await _emailService.SendEmailAsync(strTo, strFrom, accessToken, "request", strBody);
                }

				if (isResult)
				{
					return Json(new { status = 200, message = "Sent email successufully"});
				}
			}
			catch (Exception ex)
			{
                Console.WriteLine($"Send Email Exception {ex.Message}");
				_logger.LogError(ex.Message);
			}
			return Json(new { status = -200, message = "Sending email is failed!" });
		}

        [HttpPost]
        public async Task<IActionResult> RequestShopify(long orderId, int type, long em_idx)
        {
            var user = await _userManager.GetUserAsync(User);
            try
            {
                if (type ==2)
                {
                    bool isResult = await _shopifyService.CancelOrder(orderId);

                    TbOrder p = await _shopifyService.OrderRequest(orderId);

                    if (isResult)
                    {
                        if (em_idx != 0)
                        {
                            await _emailService.ChangeState(em_idx, 3);
                        }
                    }

                    return Json(new { status = isResult? 201: -201, order = p });
                } else if (type == 3)
                {
                    bool isResult = await _shopifyService.RefundOrder(orderId);
                    if (isResult)
                    {
                        if(em_idx !=0)
                        {
                            await _emailService.ChangeState(em_idx, 2);
                        }
                    }

                    TbOrder p = _shopifyService.GetOrderInfo(orderId);
                    string orderDetail = await _shopifyService.GetOrderInfoRequest(p.or_id);
                    return Json(new { status = isResult ? 201 : -201, order = p, orderDetail = orderDetail });
                }
            } catch ( Exception ex)
            {
                Console.WriteLine(ex.Message);
            }
            return Json(new { status = 0 });
        }
    }

    public class Customer
    {
        public string FirstName { get; set; }
        public string LastName { get; set; }
        public string Email { get; set; }
        public string Phone { get; set; }
        public string shippingAddress { get; set; }
        public string BillingAddress { get; set; }
        public Customer()
        {
            FirstName = "";
            LastName = "";
            Email = "";
            Phone = "";
            shippingAddress = "";
            BillingAddress = "";
        }
    }
}
