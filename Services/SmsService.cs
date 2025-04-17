using GoogleLogin.Models;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using System.Data.Entity;
using Twilio.Clients;
using Twilio.Rest.Api.V2010.Account;
using DateTime = System.DateTime;
using PhoneNumber = Twilio.Types.PhoneNumber;

namespace GoogleLogin.Services
{
    public class SmsService
    {
        private readonly IServiceScopeFactory           _serviceScopeFactory;
        private readonly ILogger<SmsService>            _logger;
        private readonly TwilioRestClient               _twilioClient;
        private readonly IHubContext<DataWebsocket>     _hubContext;

        public SmsService(
            TwilioRestClient            twilioClient, 
            IServiceScopeFactory        serviceScopeFactory, 
            ILogger<SmsService>         logger, 
            IHubContext<DataWebsocket>  hubContext)
        {
            _twilioClient           =   twilioClient;
            _serviceScopeFactory    =   serviceScopeFactory;
            _logger                 =   logger;
            _hubContext             =   hubContext;
        }

        public List<TbSms> GetSmsList(string strUserPhone)
        {
            try
            {
                using (var scope = _serviceScopeFactory.CreateScope())
                {
                    var dbContext = scope.ServiceProvider.GetRequiredService<AppIdentityDbContext>();
                    string strPhone = NormalizePhoneNumber_(strUserPhone);
                    var rawEmails = dbContext.TbSmss.Where(e => e.sm_to == strPhone).ToList();

                    var lstFrom = rawEmails
                        .GroupBy(e => e.sm_from)
                        .Select(g => new EmailDto
                        {
                            em_from = g.Key,
                            em_date = g.Max(e => e.sm_date)
                        })
                        .OrderByDescending(e => e.em_date)
                        .ToList();

                    List<TbSms> lstResult = new List<TbSms>();
                    foreach (var item in lstFrom)
                    {
                        var result = dbContext.TbSmss.Where(e => e.sm_from == item.em_from && e.sm_date == item.em_date).FirstOrDefault();
                        if (result == null) continue;
                        lstResult.Add(result);
                    }
                    return lstResult;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.StackTrace);
            }
            return new List<TbSms>();
        }

        public List<TbSms> GetSmsHistory( string strFromPhone, string strToPhone )
        {
            strFromPhone = strFromPhone.Contains("+") ? strFromPhone : "+" + strFromPhone;
            strToPhone   = strFromPhone.Contains("+") ? strToPhone   : "+" + strToPhone;
            
            try
            {
                using (var scope = _serviceScopeFactory.CreateScope())
                {
                    var dbContext = scope.ServiceProvider.GetRequiredService<AppIdentityDbContext>();
                    strToPhone   = NormalizePhoneNumber_(strToPhone);
                    strFromPhone = NormalizePhoneNumber_(strFromPhone);

                    List<TbSms> lst = dbContext
                        .TbSmss.Where(e => ((e.sm_from == strToPhone && e.sm_to == strFromPhone) 
                                    || (e.sm_to == strToPhone && e.sm_from == strFromPhone))
                                    && !string.IsNullOrEmpty(e.sm_body))
                        .OrderBy(e => e.sm_date)
                        .ToList();

                    return lst;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.StackTrace);
                return new List<TbSms>();
            }
        }

        public async Task<TbSms?> GetSmsById(string strId)
        {
            TbSms? sms;

			if (string.IsNullOrEmpty(strId)) return new TbSms();
			
			using (var scope = _serviceScopeFactory.CreateScope())  // Create a new scope
			{
				var _dbContext = scope.ServiceProvider.GetRequiredService<AppIdentityDbContext>();

				sms = _dbContext
                    .TbSmss
                    .Where(e => e.sm_id == strId && e.sm_date != null)
                    .OrderBy(e => e.sm_date)
                    .FirstOrDefault() ?? null;
                if (sms == null) 
                    return null;
                sms.sm_read = 1;
				await _dbContext.SaveChangesAsync();
				return sms;
			}
		}

        public async Task<List<TbSms>> GetSms(string strMyPhone, string strFromPhone)
        {
            if (string.IsNullOrEmpty(strMyPhone) || string.IsNullOrEmpty(strFromPhone)) return new List<TbSms>();

            strMyPhone = NormalizePhoneNumber_(strMyPhone);
            strFromPhone = NormalizePhoneNumber_(strFromPhone);
            using (var scope = _serviceScopeFactory.CreateScope())  // Create a new scope
            {
                var _dbContext = scope.ServiceProvider.GetRequiredService<AppIdentityDbContext>();
                
				List<TbSms> lst = await _dbContext.TbSmss
	                .Where(e =>
		                (e.sm_to == strMyPhone && e.sm_from == strFromPhone) ||
		                (e.sm_to == strFromPhone && e.sm_from == strMyPhone) && e.sm_date != null)
	                .OrderBy(e => e.sm_date)
	                .ToListAsync();
				
                lst.ForEach(e => e.sm_read = 1);

                await _dbContext.SaveChangesAsync();
                return lst;
            }
        }
        
        public async Task<int> SendSms(string strToPhone, string strFromPhone, string strSms)
        {
            if (string.IsNullOrEmpty(strSms) || string.IsNullOrEmpty(strToPhone)) 
                return -1;

            // Send SMS
            var messageResponse = MessageResource.Create(
                body: strSms,
                from: new Twilio.Types.PhoneNumber(strFromPhone),
                to: new Twilio.Types.PhoneNumber(strToPhone),
                client: _twilioClient
            );

            if(messageResponse != null)
            {
                TbSms p = new TbSms
                {
                    sm_id = messageResponse.Sid,
                    sm_to = messageResponse.To,
                    sm_from = messageResponse.From.ToString(),
                    sm_body = messageResponse.Body,
                    sm_date = messageResponse.DateSent == null ? DateTime.Now : messageResponse.DateSent,
                    sm_read = 1
                };
                await SaveSms(p);

                return 1;
            }
            return -1;
        }

        public async Task<string> GetLastSms(string strPhoneNumber, string strMyPhone)
        {
            using (var scope = _serviceScopeFactory.CreateScope())  // Create a new scope
            {
                var _dbContext = scope.ServiceProvider.GetRequiredService<AppIdentityDbContext>();
                strPhoneNumber = NormalizePhoneNumber_(strPhoneNumber);
                strMyPhone = NormalizePhoneNumber_(strMyPhone);
                var lastMessage = await _dbContext.TbSmss.Where(m => m.sm_to == strPhoneNumber && m.sm_from == strMyPhone).OrderByDescending(m => m.sm_date).FirstOrDefaultAsync();
                if(lastMessage == null)
                {
                    List<string> lstBody = await _dbContext.TbSmss.Where(m => m.sm_from == strPhoneNumber && m.sm_to == strMyPhone).Select(m => m.sm_body).ToListAsync();
                    return string.Join("\n", lstBody);
                }
                else
                {
                    List<string> lstBody = await _dbContext.TbSmss.Where(m => m.sm_from == strPhoneNumber && m.sm_date > lastMessage.sm_date && m.sm_to == strMyPhone).Select(m => m.sm_body).ToListAsync();
                    return string.Join("\n", lstBody);
                }
            }
        }

        public async Task<List<string>> GetPhoneList(string strMyPhone)
        {
            using (var scope = _serviceScopeFactory.CreateScope()) 
            {
                var _dbContext = scope.ServiceProvider.GetRequiredService<AppIdentityDbContext>();
                List<string> lstFrom    = await _dbContext
                                                .TbSmss
                                                .OrderByDescending(e => e.sm_date)
                                                .Select(e => e.sm_from)
                                                .ToListAsync();
                List<string> lstTo      = await _dbContext
                                                .TbSmss.OrderByDescending(e => e.sm_date)
                                                .Select(e => e.sm_to)
                                                .ToListAsync();
                List<string> lstResult  = lstFrom.Union(lstTo).ToList();
                lstResult.RemoveAll(item => NormalizePhoneNumber_(item) == NormalizePhoneNumber_(strMyPhone));
                return lstResult;
            }
        }

        public async Task GetMessages(string strPhoneNumber)
        {
            var messages = await MessageResource.ReadAsync(                
                dateSentAfter: new DateTime(DateTime.Now.Year, DateTime.Now.Month, 1, 0, 0, 0), limit: 500, client: _twilioClient);
            Console.WriteLine(messages);
            Console.WriteLine("***************PKH: Twillow update step 1*****************");
            foreach (var message in messages)
            {
                Console.WriteLine("***************PKH: Twillow update step 1*****************");
                if (message == null) continue;
                Console.WriteLine("***************PKH: Twillow update step 2*****************");
                TbSms p = new TbSms
                {
                    sm_id = message.Sid,
                    sm_to = message.To.ToString(),
                    sm_from = message.From.ToString(),
                    sm_body = message.Body,
                    sm_date = message.DateSent,
                    sm_state = 0,
                    sm_read = 0
                };
                await SaveSms(p);
                Console.WriteLine("***************PKH: Twillow update step 3*****************");
            }            
        }

        public async Task SaveSms(TbSms p)
        {
            if (p == null) return;
            using (var scope = _serviceScopeFactory.CreateScope())  // Create a new scope
            {
                var _dbContext = scope.ServiceProvider.GetRequiredService<AppIdentityDbContext>();
                TbSms _p = _dbContext.TbSmss.Where(e => e.sm_id == p.sm_id).FirstOrDefault();
                if (_p == null)
                {
                    _dbContext.TbSmss.Add(p);
                    await _dbContext.SaveChangesAsync();
                }
            }
        }

        private string DateTimeDiff(TimeSpan timeSpan)
        {
            if (timeSpan.Days > 0)
                return $"{timeSpan.Days} day{(timeSpan.Days > 1 ? "s" : "")} ago";
            if (timeSpan.Hours > 0)
                return $"{timeSpan.Hours} hour{(timeSpan.Hours > 1 ? "s" : "")} ago";
            if (timeSpan.Minutes > 0)
                return $"{timeSpan.Minutes} minute{(timeSpan.Minutes > 1 ? "s" : "")} ago";
            return $"{timeSpan.Seconds} second{(timeSpan.Seconds > 1 ? "s" : "")} ago";
        }

		public CustomerInfo GetCustomerInfo(string sm_id)
		{
			CustomerInfo obj = new CustomerInfo();
			try
			{
				using (var scope = _serviceScopeFactory.CreateScope())
				{
					var dbContext = scope.ServiceProvider.GetRequiredService<AppIdentityDbContext>();

					TbSms p = dbContext.TbSmss.Where(e => e.sm_id == sm_id).FirstOrDefault();
					if (p == null) return null;
					obj.strEmail = p.sm_from;

					obj.strSubject = $"New customer message " ;
					obj.strSubject = $"{obj.strSubject} on {p.sm_date?.ToString("MMM dd, hh:mm")}";


					List<TbShopifyUser> _users = dbContext.TbShopifyUsers.Where(e => !string.IsNullOrEmpty(p.sm_from) && !string.IsNullOrEmpty(e.phone)).ToList();
                    TbShopifyUser _user = null;
                    foreach(var user in _users) { 
                        PhoneNumber p1 = new PhoneNumber(user.phone);
                        PhoneNumber p2 = new PhoneNumber(p.sm_from);
                        if(p1.ToString() == p2.ToString())
                        {
                            _user = user;
                            break;
                        }
                    }
					if (_user == null) return obj;
					obj.strName = _user.UserName;
					obj.strPhone = _user.phone;
					return obj;
				}
			}
			catch (Exception ex)
			{
				Console.WriteLine("modelservice/getcustomerinfo " + ex.StackTrace);
			}
			return obj;
		}

        public int GetSMSListPerUserCount(string myPhone, int nPerPage)
        {
            try
            {
                using (var scope = _serviceScopeFactory.CreateScope())
                {
                    var dbContext = scope.ServiceProvider.GetRequiredService<AppIdentityDbContext>();
                    string strPhone = NormalizePhoneNumber_(myPhone);
                    var rawEmails = dbContext.TbSmss.Where(e => e.sm_to == strPhone).ToList();

                    var lstFrom = rawEmails
                        .GroupBy(e => e.sm_from)
                        .Select(g => new EmailDto
                        {
                            em_from = g.Key,
                            em_date = g.Max(e => e.sm_date)
                        })
                        .Count();

                    return lstFrom / nPerPage + 1;                    
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.StackTrace);
            }
            return 0;
        }

        public async Task SendSmsCountInfo(string strMyPhone)
        {
            using (var scope = _serviceScopeFactory.CreateScope())  // Create a new scope
            {
                var _dbContext = scope.ServiceProvider.GetRequiredService<AppIdentityDbContext>();
                strMyPhone = NormalizePhoneNumber_(strMyPhone);

                var result = _dbContext.TbSmss
                        .Where(e => EF.Functions.Like(e.sm_to, $"%{strMyPhone}%"))
                        .AsEnumerable() 
                        .GroupBy(e => 1)
                        .Select(g => new MailInfo
                        {
                            nCntWhole = g.Count(),
                            nCntRead = g.Count(e => e.sm_read == 1),
                            nCntUnread = g.Count(e => e.sm_read == 0),
                            nCntOnTime = g.Count(e => e.sm_state == 1 || (e.sm_state == 0 && e.sm_date.HasValue && (DateTime.Now - e.sm_date.Value).Days < 7)),
                            nCntLate = g.Count(e => e.sm_state == 0 && e.sm_date.HasValue && (DateTime.Now - e.sm_date.Value).Days >= 7 && (DateTime.Now - e.sm_date.Value).Days < 30),
                            nCntDanger = g.Count(e => e.sm_state == 0 && e.sm_date.HasValue && (DateTime.Now - e.sm_date.Value).Days > 30),
                            nCntArchived = g.Count(e => e.sm_state == 3),
                        })
                        .FirstOrDefault();

                var nCntReply = _dbContext.TbSmss.Where(e => EF.Functions.Like(e.sm_from, $"%{strMyPhone}%")).AsEnumerable().Count();

                var _result = new
                {
                    nCntWhole       = result?.nCntWhole,
                    nCntRead        = result?.nCntRead,
                    nCntUnread      = result?.nCntUnread,
                    nCntOnTime      = result?.nCntOnTime,
                    nCntLate        = result?.nCntLate,
                    nCntDanger      = result?.nCntDanger,
                    nCntArchived    = result?.nCntArchived,
                    nCntReply       = nCntReply
                };

                var objPacket = new
                {
                    SMSInfo = _result,
                    type = "sms"
                };
                string strJson = System.Text.Json.JsonSerializer.Serialize(objPacket);
                await _hubContext.Clients.All.SendAsync("ReceiveMessage", "", strJson);
            }            
        }

        public async Task SendNewSmsInfo(TbSms p)
        {
            var objPacket = new
            {
                SMSInfo = p,
                type = "new_sms"
            };
            string strJson = System.Text.Json.JsonSerializer.Serialize(objPacket);
            await _hubContext.Clients.All.SendAsync("ReceiveMessage", "", strJson);
        }

        public async Task<bool> ChangeState(List<string> lstIds, int em_state)
        {
            try
            {
                using (var scope = _serviceScopeFactory.CreateScope())
                {
                    var dbContext = scope.ServiceProvider.GetRequiredService<AppIdentityDbContext>();

                    List<TbSms?> lst = dbContext.TbSmss.Where(e => lstIds.Contains(e.sm_id)).ToList();
                    foreach (var _one in lst)
                    {
                        _one.sm_state = em_state;
                    }
                    await dbContext.SaveChangesAsync();
                    return true;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.StackTrace);
            }
            return false;
        }

        string NormalizePhoneNumber_(string phoneNumber)
		{
			string digitsOnly = new string(phoneNumber.Where(char.IsDigit).ToArray());

			if (digitsOnly.Length >= 10) 
			{
				return "+" + digitsOnly;
			}

			return digitsOnly;
		}
	}
    public class SmsInfo
    {
        public string From { get; set; }
        public string Body { get; set; }
        public string DtString { get; set; }
    }
}
