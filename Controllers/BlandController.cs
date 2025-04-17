using GoogleLogin.Models;
using GoogleLogin.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NuGet.Protocol;
using System.Net.Http.Headers;

namespace GoogleLogin.Controllers
{

    //[Authorize]
    public class BlandController : Controller
    {
        private readonly UserManager<AppUser>   _userManager;
        private readonly ILogger<SmsController> _logger;
        private readonly LLMService             _llmService;
        private readonly string                 _phoneNumber;
        private readonly ShopifyService         _shopifyService;
        private readonly SmsService             _smsService;
        private string                          _blandUrl;
        private string                          _authorization;
        private string                          _encryptKey;
        private string                          _domain;

        public BlandController(
            UserManager<AppUser>    userManager,
            LLMService              llmService,
            SmsService              smsService,
            ILogger<SmsController>  logger,
            IConfiguration          configuration,
            ShopifyService          shopifyService)
        {
            _userManager    =   userManager;
            _llmService     =   llmService;
            _smsService     =   smsService;
            _logger         =   logger;
            _domain         =   configuration["Domain"] ?? string.Empty;
            _phoneNumber    =   configuration["bland:phoneNumber"] ?? string.Empty;
            _authorization  =   configuration["bland:authorization"] ?? string.Empty;
            _encryptKey     =   configuration["bland:encryptKey"] ?? string.Empty;
            _shopifyService =   shopifyService;
            _blandUrl       =   $"https://api.bland.ai/v1/inbound/{_phoneNumber}";
        }

        [HttpGet]
        public IActionResult Index()
        {
            return View("View_BlandManage");
        }

        [HttpPost("/configurebland")] 
        public async Task<IActionResult> configureBland(string prompt)
        {
            Console.WriteLine($"Controller : bland action : configurebland");

            if (_authorization.IsNullOrEmpty())
                return Json(new { status = -201, message = "Bland authorization is empty" });

            if (_encryptKey.IsNullOrEmpty())
                return Json(new { status = -201, message = "Bland encrypt key is empty" });

            if (_phoneNumber.IsNullOrEmpty())
                return Json(new { staus = -201, message = "Bland phone number is empty" });

            prompt = "Goal: Receiving call from customers. They want to refund(cancel, know or else) their order. Confirm the order id and what they want with their order.\r\n\r\nCall Flow:\r\n1. Introduce yourself and say greeting.\r\n2. Ask customer what he/she wants with his/her order?\r\n3. Confirm order id.\r\n4. Greeting.\r\n\r\nBackground:\r\nMy name is Sherman. I am an assistant of attentify customer service. I receive a call from customer who they want something with their order.\r\nI confirm the order id and what they are looking for with.\r\n\r\nHere’s an example dialogue:\r\n\r\nYou: Thank you for calling attentify customer service? My name is Sherman, how can I assist you today?\r\nPerson: Hi Sherman, I need some help with an order I recently placed.\r\nYou: Sure, I'd be happy to help. Could I get your name and order number to pull up your account?\r\nPerson: It's John Smith, order #12345\r\nYou: Thanks for that information. What do you need with your order?\r\nPerson: I want to refund the order.(I want to know the status of the order or I want to cancel my order)\r\nYou: Got it. I will process it. You should receive a confirmation email shortly.\r\nPerson: Thank you, I really appreciate you taking care of this so quickly!\r\nYou: You're very welcome! Thank you for being a valued attentify customer. Please reach back out if you have any other issues with your order.\r\n    Have a great rest of your day!\r\nPerson: You too, bye!";
            
            var client = new HttpClient();
            var request = new HttpRequestMessage(HttpMethod.Post, _blandUrl);
            request.Headers.Add("Authorization", _authorization);
            request.Headers.Add("encrypted_key", _encryptKey);
            //org_dffbc34bfffb310f4f2fae9c053b6e663803512b77de2d03849589597b5a98a313499da5f9011c6fd36b69

            var payload = new
            {
                prompt = prompt,
                pathway_id = string.Empty,
                voice = "josh",
                background_track = "office",
                first_sentence = string.Empty,
                wait_for_greeting = true,
                interruption_threshold = 123,
                model = "enhanced",
                tools = Array.Empty<Object>(),
                language = "en-US",
                timezone = "America/Los_Angeles",
                //transfer_phone_number = string.Empty,
                //transfer_list = new {},
                //dynamic_data = new[] { "" },
                //keywords = Array.Empty<string>(),
                max_duration = 123,
                webhook = $"{_domain}receivebland",
                //analysis_schema = new {},
                //metadata = new { },
                //summary_prompt = string.Empty,
                //analysis_prompt = "",
                record = true
            };
            string jsonContent = JsonConvert.SerializeObject(payload);

            var content = new StringContent(jsonContent);
            content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
            request.Content = content;

            var response = await client.SendAsync(request);
            Console.WriteLine(response.ToJson());
            if (response.IsSuccessStatusCode)
            {
                return Json(new { status = 201, message = "Updated bland configuration" });
            }
            else
            {
                return Json(new { status = 201, message = "Failed updating bland configuration" });
            }
        }

        [HttpPost("/deletebland")]
        public async Task<IActionResult> deleteBland(string prompt)
        {
            Console.WriteLine($"Prompt is :{prompt}");

            var client = new HttpClient();
            var request = new HttpRequestMessage(HttpMethod.Post, $"{_blandUrl}/delete");
            request.Headers.Add("Authorization", _authorization);
            request.Headers.Add("encrypted_key", _encryptKey);

            var payload = new
            {
            };
            string jsonContent = JsonConvert.SerializeObject(payload);

            var content = new StringContent(jsonContent);
            content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
            request.Content = content;

            var response = await client.SendAsync(request);

            if (response.IsSuccessStatusCode)
            {
                return Json(new { status = 201, message = "Deleted bland phone number" });
            }
            else
            {
                return Json(new { status = 201, message = "Failed deleing bland phone number" });
            }
        }

        [HttpPost("/insertbland")]
        public async Task<IActionResult> insertBland(string prompt)
        {
            Console.WriteLine($"Prompt is :insert");

            var client = new HttpClient();
            var request = new HttpRequestMessage(HttpMethod.Post, $"https://api.bland.ai/v1/inbound/insert");
            request.Headers.Add("Authorization", _authorization);
            request.Headers.Add("encrypted_key", _encryptKey);

            var payload = new
            {
                numbers = new[] { "+18888179263" }
            };
            string jsonContent = JsonConvert.SerializeObject(payload);

            var content = new StringContent(jsonContent);
            content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
            request.Content = content;

            var response = await client.SendAsync(request);

            if (response.IsSuccessStatusCode)
            {
                return Json(new { status = 201, message = "Inserted bland phone number" });
            }
            else
            {
                return Json(new { status = 201, message = "Failed inserting bland phone number" });
            }
        }

        [HttpPost("/receivebland")]
        public async Task<IActionResult> ReceiveBland()
        {
            Console.WriteLine("Hooked by bland");
            using (var reader = new StreamReader(Request.Body))
            {
                string requestBody = await reader.ReadToEndAsync();
                Console.WriteLine($"Received from bland: {requestBody}");
                JObject jsonObject = JObject.Parse(requestBody);

                string callId = jsonObject["call_id"]?.ToString() ?? string.Empty;
                string from = jsonObject["from"]?.ToString() ?? string.Empty;
                string to = jsonObject["to"]?.ToString() ?? string.Empty; 

                List<TbSms> extractedData = new List<TbSms>();
                JArray transcripts = (JArray)jsonObject["transcripts"];

                if ( transcripts != null )
                {
                    foreach( var item in transcripts)
                    {
                        var user = item["user"]?.ToString() ?? string.Empty;

                        TbSms p = new TbSms
                        {
                            sm_id   = callId,
                            sm_from = user == "assistant" ? to : from,
                            sm_to   = user == "assistant" ? from : to,
                            sm_body = item["text"]?.ToString() ?? string.Empty,
                            sm_date = DateTime.Now,
                            sm_read = 0
                        };

                        await _smsService.SaveSms(p);
                        Console.WriteLine("Save Sms");
                    }
                }
            }

            return Ok();
        }
    }    
}
