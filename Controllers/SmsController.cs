using GoogleLogin.Models;
using GoogleLogin.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.IdentityModel.Tokens;
using Newtonsoft.Json.Linq;
using System.Data;
using Twilio.TwiML;
using Twilio.TwiML.Messaging;
using Twilio.TwiML.Voice;

namespace GoogleLogin.Controllers
{

    //[Authorize]
    public class SmsController : Controller
    {
        private readonly UserManager<AppUser>   _userManager;
        private readonly ILogger<SmsController> _logger;
        private readonly SmsService             _smsService;
        private readonly LLMService             _llmService;
        private readonly string                 _phoneNumber;
        private readonly ShopifyService         _shopifyService;
        public SmsController(
            UserManager<AppUser>        userManager,
            LLMService                  llmService,
            SmsService                  smsService,
            ILogger<SmsController>      logger,
            IConfiguration              configuration,
            ShopifyService              shopifyService)
        {
            _userManager    = userManager;
            _llmService     = llmService;
            _logger         = logger;
            _phoneNumber    = "+18888179263";
            _smsService     = smsService;
            _shopifyService = shopifyService;
        }

        [HttpGet]
        public IActionResult Index(string phone)
        {
            ViewBag.menu       = "sms";
            ViewBag.strMyPhone = _phoneNumber;
            return View();
        }

        [HttpPost]
        public IActionResult GetSmsList(string strToPhone)
        {
            var smsList     = _smsService.GetSmsList(strToPhone);
            ViewBag.smsList = smsList;

            return PartialView("View_smsList");
        }

        [HttpPost]
        public IActionResult GetSmsHistory(string strFromPhone, string strToPhone)
        {
            var smsHistory = _smsService.GetSmsHistory(strFromPhone, strToPhone);
            ViewBag.strMyPhone = strToPhone;
            ViewBag.smsHistory = smsHistory;

            return PartialView("View_smsHistory");
        }

        [HttpPost]
        public async Task<IActionResult> InitializeSms(string phoneNumber)
        {
            AppUser? user = await _userManager.GetUserAsync(HttpContext.User);
            string? myPhone = _phoneNumber;

            if (user != null && !string.IsNullOrEmpty(user.PhoneNumber))
                myPhone = user.PhoneNumber;

            ViewBag.phone           = phoneNumber;
            ViewBag.myPhoneNumber   = myPhone;
            ViewBag.smsList         = await _smsService.GetSms(myPhone, phoneNumber);

            await _smsService.SendSmsCountInfo(myPhone);
            return PartialView("Sms");
        }

        [HttpPost("Sms/response")]
        public async Task<IActionResult> ResponseSms(string phone)
        {
            AppUser? user = await _userManager.GetUserAsync(HttpContext.User);

            string? myPhone = _phoneNumber;
            if (user != null && !string.IsNullOrEmpty(user.PhoneNumber))
                myPhone = user.PhoneNumber;

            string strBody = await _smsService.GetLastSms(phone, myPhone);
            if (string.IsNullOrWhiteSpace(strBody))
            {
                return Json(new { status = 0 });
            }

            string strRespond = await _llmService.GetResponseAsync(strBody);
            JObject jsonObj = JObject.Parse(strRespond);
            int status = (int)jsonObj["status"];
            if (status == 0)    //mail not contain order info.
            {
                string strMail = jsonObj["msg"].ToString();
                if (string.IsNullOrEmpty(strMail))
                {
                    strMail = "Hello! \n Could you please send me the correct message containing the order information?";
                    return Json(new { status = 1, data = new { rephase = new { msg = strMail } } });
                }
                return Json(new { status = 1, data = new { rephase = strRespond } });
            }
            else //mail contain order info.
            {
                string strMail = "Hello! \n I will consider your request. May I process your order request for you?";
                return Json(new { status = 1, data = new { rephase = new { msg = strMail } } });
            }
        }

        [HttpPost]
        public async Task<IActionResult> SendSms(string strToPhone, string strFromPhone, string strSms )
        {
            if (strToPhone.IsNullOrEmpty())
                return Json(new { status = -201, message = "Client phone number is empty" });

            if (strFromPhone.IsNullOrEmpty())
                return Json(new { status = -201, message = "Business phone number is empty" });

            if (strSms.IsNullOrEmpty())
                return Json(new { status = -201, message = "Sms is empty" });

            int nRet = await _smsService.SendSms(strSms, strToPhone, strFromPhone);

            if (nRet == 1)
            {
                return Json(new { status = 201, message = "Sent sms successfully." });
            } else
            {
                return Json(new { status = -201, message = "Failed sending sms." });
            }
        }

        [HttpPost]
        public async Task<IActionResult> ParseAI(string phone)
        {
            try
            {
                AppUser? user = await _userManager.GetUserAsync(HttpContext.User);
                string? myPhone = _phoneNumber;
                if (user != null && !string.IsNullOrEmpty(user.PhoneNumber))
                    myPhone = user.PhoneNumber;

                string strBody = await _smsService.GetLastSms(phone, myPhone);
                if (string.IsNullOrWhiteSpace(strBody))
                {
                    return Json(new { status = -1, data = new { rephase = new { msg = "There is no request in the message." } } });
                }
                string strRespond = await _llmService.GetResponseAsync(strBody);
                JObject jsonObj = JObject.Parse(strRespond);
                int status = (int)jsonObj["status"];

                if (status == 0)
                {
                    string strMail = jsonObj["msg"].ToString();
                    TbOrder p = _shopifyService.GetOrderInfoByPhone(phone);
                    if (p == null)
                    {
                        return Json(new { status = -1, data = new { rephase = new { msg = "There is no order information available." } } });
                    }
                    else
                    {
                        string orderDetail = await _shopifyService.GetOrderInfoRequest(p.or_id);
                        return Json(new { status = 4, data = new { orderId = p.or_id, order = p, orderDetail = orderDetail } });
                    }
                }
                else
                {
                    string strType = jsonObj["type"].ToString();
                    string strOrderId = jsonObj["order_id"].ToString();
                    if (!string.IsNullOrEmpty(strOrderId))
                    {
                        TbOrder p = _shopifyService.GetOrderInfo(strOrderId);
                        if (p == null)
                        {
                            p = _shopifyService.GetOrderInfoByPhone(phone);
                            if (p == null)
                            {
                                return Json(new { status = -1, data = new { rephase = new { msg = "There is no order information available." } } });
                            }
                        }
                        string orderDetail = await _shopifyService.GetOrderInfoRequest(p.or_id);
                        return Json(new { status = 4, data = new { orderId = strOrderId, order = p, orderDetail = orderDetail } });
                    }
                }
                return Json(new { status = 0, data = new { msg = "" } });
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
                return Json(new { status = 0 });
            }
        }

        [HttpPost("/smsreceive")]
        public async Task<IActionResult> ReceiveSms([FromForm] string from, [FromForm] string to, [FromForm] string body, [FromForm] string messageSid)
        {
            Console.WriteLine($"Received SMS from {from}: {body}");
            Console.WriteLine($"Received SMS to {to}: {body}");
            Console.WriteLine($"Received SMS id: {messageSid}");
            _logger.LogInformation($"Received SMS from {from}: {body}");

            TbSms p = new TbSms
            {
                sm_id   = messageSid,
                sm_from = from,
                sm_body = body,
                sm_to   = to,
                sm_date = DateTime.Now,
                sm_read = 0
            };

            await _smsService.SaveSms(p);
            await _smsService.SendSmsCountInfo(to);
            Thread.Sleep(10);
            await _smsService.SendNewSmsInfo(p);
            return NoContent();
        }
    }    
}
