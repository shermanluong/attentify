using GoogleLogin.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.SignalR;

namespace GoogleLogin.Services
{
	public class DataWebsocket : Hub
	{
        private readonly EMailService           _emailService;
        private readonly UserManager<AppUser>   _userManager;
        private readonly SmsService             _smsService;
        private readonly string                 _phoneNumber;
        public DataWebsocket(
            EMailService eMailService, 
            ShopifyService shopifyService, 
            UserManager<AppUser> userManager,
            SmsService smsService, 
            IConfiguration _configuration)
        {
            _emailService = eMailService;
            _userManager = userManager;
            _smsService = smsService;
            _phoneNumber = _configuration["Twilio:PhoneNumber"] ?? "";
        }

        public async Task SendMessage(string user, string message)
		{
            await Clients.Caller.SendAsync("ReceiveMessage", user, message);
            //await Clients.Others.SendAsync("ReceiveMessage", user, message);
		}

        public override async Task OnConnectedAsync()
        {
            Thread.Sleep(10);

            await SendMailInfo();

            Thread.Sleep(10);

            await SendSMSInfo();

            _ = base.OnConnectedAsync();
        }

        public override Task OnDisconnectedAsync(Exception exception)
        {
            Console.WriteLine("Client disconnected: " + Context.ConnectionId);
            return base.OnDisconnectedAsync(exception);
        }

        public async Task SendMailInfo()
        {
            var httpContext = this.Context.GetHttpContext();
            AppUser? user = await _userManager.GetUserAsync(httpContext.User);
            if(user != null && !string.IsNullOrEmpty(user.Email))
            {
                MailInfo p =  _emailService.GetMailCount(user.Email);
                var objPacket = new
                {
                    MailInfo = p,
                    type = "mail"
                };
                string strJson = System.Text.Json.JsonSerializer.Serialize(objPacket);
                await SendMessage("", strJson);
            }
        }


        public async Task SendSMSInfo()
        {
            var httpContext = this.Context.GetHttpContext();
            AppUser? user = await _userManager.GetUserAsync(httpContext.User);
            if (user != null)
            {
                string strPhone = string.IsNullOrEmpty(user.PhoneNumber) ? _phoneNumber : user.PhoneNumber;
                await _smsService.SendSmsCountInfo(strPhone);                
            }
        }

        //public async Task SendStoreInfo()
        //{
        //    List<OrderRequest> ptRequest = await _shopifyService.UpdateOrder();
        //    var objPacket = new
        //    {
        //        orders = ptRequest,
        //        type = "store"
        //    };
        //    string strJson = System.Text.Json.JsonSerializer.Serialize(objPacket);
        //    await SendMessage("", strJson);
        //}
    }
}
