using Azure.Core;
using Google.Apis.Auth.OAuth2;
using Google.Apis.Gmail.v1;
using Google.Apis.Gmail.v1.Data;
using Google.Apis.Services;
using GoogleLogin.Models;
using Microsoft.AspNet.Identity;
using Microsoft.EntityFrameworkCore;
using System.Net.Mail;
using System.Text;

namespace GoogleLogin.Helpers
{
    public class EmailHelper
    {
        // other methods

        public bool SendEmailTwoFactorCode(string userEmail, string code)
        {
            MailMessage mailMessage = new MailMessage();
            mailMessage.From = new MailAddress("care@yogihosting.com");
            mailMessage.To.Add(new MailAddress(userEmail));

            mailMessage.Subject = "Two Factor Code";
            mailMessage.IsBodyHtml = true;
            mailMessage.Body = code;

            SmtpClient client = new SmtpClient();
            client.Credentials = new System.Net.NetworkCredential("care@yogihosting.com", "yourpassword");
            client.Host = "smtpout.secureserver.net";
            client.Port = 80;

            try
            {
                client.Send(mailMessage);
                return true;
            }
            catch
            {
                // log exception
            }
            return false;
        }

        public bool SendEmail(string userEmail, string confirmationLink)
        {
            MailMessage mailMessage = new MailMessage();
            mailMessage.From = new MailAddress("care@yogihosting.com");
            mailMessage.To.Add(new MailAddress(userEmail));

            mailMessage.Subject = "Confirm your email";
            mailMessage.IsBodyHtml = true;
            mailMessage.Body = confirmationLink;

            SmtpClient client = new SmtpClient();
            client.Credentials = new System.Net.NetworkCredential("care@yogihosting.com", "yourpassword");
            client.Host = "smtpout.secureserver.net";
            client.Port = 80;

            try
            {
                client.Send(mailMessage);
                return true;
            }
            catch
            {
                // log exception
            }
            return false;
        }

        public bool SendEmailPasswordReset(string userEmail, string link)
        {
            MailMessage mailMessage = new MailMessage();
            mailMessage.From = new MailAddress("care@yogihosting.com");
            mailMessage.To.Add(new MailAddress(userEmail));

            mailMessage.Subject = "Password Reset";
            mailMessage.IsBodyHtml = true;
            mailMessage.Body = link;

            SmtpClient client = new SmtpClient();
            client.Credentials = new System.Net.NetworkCredential("care@yogihosting.com", "yourpassword");
            client.Host = "smtpout.secureserver.net";
            client.Port = 80;

            try
            {
                client.Send(mailMessage);
                return true;
            }
            catch
            {
                // log exception
            }
            return false;
        }

        public async static Task GetMailList(AppIdentityDbContext _context, string accessToken)
        {
            // Use the access token to create a credential object
            var credential = GoogleCredential.FromAccessToken(accessToken);

            // Create the Gmail service
            var service = new GmailService(new BaseClientService.Initializer()
            {
                HttpClientInitializer = credential,
                ApplicationName = "MSCFront"
            });

            var request = service.Users.Messages.List("me");
            request.LabelIds = "INBOX";
            request.IncludeSpamTrash = true;
            request.MaxResults = 500;
            var messagesResponse = await request.ExecuteAsync();

            var emails = new List<string>();

            if (messagesResponse.Messages != null && messagesResponse.Messages.Count > 0)
            {
                foreach (var msg in messagesResponse.Messages)
                {
                    var emailInfoRequest = service.Users.Messages.Get("me", msg.Id);
                    var emailInfoResponse = await emailInfoRequest.ExecuteAsync();

                    if (emailInfoResponse != null)
                    {
                        try
                        {
                            string body = "";
                            string? _date = emailInfoResponse.Payload.Headers.Where(obj => obj.Name == "Date").FirstOrDefault()?.Value;
                            string? _from = emailInfoResponse.Payload.Headers.Where(obj => obj.Name == "From").FirstOrDefault()?.Value;
                            string? _subject = emailInfoResponse.Payload.Headers.Where(obj => obj.Name == "Subject").FirstOrDefault()?.Value;
                            string? _inReplyTo = emailInfoResponse.Payload.Headers.Where(obj => obj.Name == "In-Reply-To").FirstOrDefault()?.Value;

                            string _threadId = emailInfoResponse.ThreadId;

                            if (_date != "" && _from != null)
                            {
                                foreach (var part in emailInfoResponse.Payload.Parts)
                                {
                                    /*if(part.MimeType == "text/plain")
                                    {
                                        body = part.Body.Data;
                                        byte[] byteArray = FromBase64ForUrlString(body);
                                        body = Encoding.UTF8.GetString(byteArray);
                                    }
                                    else */
                                    if (part.MimeType == "text/html")
                                    {
                                        body = part.Body.Data;
                                        byte[] byteArray = FromBase64ForUrlString(body);
                                        body = Encoding.UTF8.GetString(byteArray);
                                    }
                                }
                            }
                            Console.WriteLine($"{_date}: {_from} : {_subject} : {body}");
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine("emailhelper/getmailist" + ex.ToString());
                        }
                    }
                }
            }
        }
        static byte[] FromBase64ForUrlString(string base64Url)
        {
            string padded = base64Url.Replace('-', '+').Replace('_', '/');
            switch (padded.Length % 4)
            {
                case 2: padded += "=="; break;
                case 3: padded += "="; break;
            }
            return Convert.FromBase64String(padded);
        }
    }
}
