using Google.Apis.Gmail.v1.Data;
using Google.Apis.Gmail.v1;
using Google.Apis.Auth.OAuth2;
using Google.Apis.Services;
using GoogleLogin.Models;
using Microsoft.Extensions.DependencyInjection;

namespace GoogleLogin.Services
{
    public class GMailWatchService
    {
        private readonly GmailService _gmailService;

        public GMailWatchService(string accessToken)
        {
            var credential = GoogleCredential.FromAccessToken(accessToken)
                .CreateScoped(GmailService.Scope.GmailReadonly);

            _gmailService = new GmailService(new BaseClientService.Initializer
            {
                HttpClientInitializer = credential,
                ApplicationName = "GoogleLogin"
            });
        }

        // Get the total number of emails
        public async Task<int> GetTotalEmailCountAsync()
        {
            var request = _gmailService.Users.Messages.List("me");
            request.MaxResults = 1; // Limit for performance
            var response = await request.ExecuteAsync();

            return (int)(response?.ResultSizeEstimate ?? 0);
        }

        // Get the count of unread emails
        public async Task<int> GetUnreadEmailCountAsync()
        {
            var request = _gmailService.Users.Messages.List("me");
            request.Q = "is:unread";
            request.MaxResults = 1;
            var response = await request.ExecuteAsync();

            return (int)(response?.ResultSizeEstimate ?? 0);
        }

        // Get the count of read emails in a specific time range
        public async Task<int> GetReadEmailCountInTimeRangeAsync(DateTime startDate, DateTime endDate)
        {
            var query = $"is:read after:{startDate:yyyy/MM/dd} before:{endDate:yyyy/MM/dd}";
            var request = _gmailService.Users.Messages.List("me");
            request.Q = query;
            request.MaxResults = 1;

            var response = await request.ExecuteAsync();
            return (int)(response?.ResultSizeEstimate ?? 0);
        }

        public async Task<List<Message>> GetMessagesAsync(GmailService service, string query = "")
        {
            var messages = new List<Message>();
            var request = service.Users.Messages.List("me");
            if (!string.IsNullOrEmpty(query))
            {
                request.Q = query;
            }
            request.MaxResults = 20;

            ListMessagesResponse response = await request.ExecuteAsync();

            if (response.Messages != null)
            {
                foreach (var msg in response.Messages)
                {
                    var message = await service.Users.Messages.Get("me", msg.Id).ExecuteAsync();
                    messages.Add(message);
                }
            }
            return messages;
        }

        //get distinct user and datetime and subject list
        public async Task<List<(string Sender, string Subject, DateTime Date)>> GetDistinctSenders()
        {
            var messages = await GetMessagesAsync(_gmailService);
            var senders = new List<(string Sender, string Subject, DateTime Date)>();

            foreach (var message in messages)
            {
                var headers = message.Payload.Headers;
                var fromHeader = headers.FirstOrDefault(h => h.Name == "From");
                var dateHeader = headers.FirstOrDefault(h => h.Name == "Date");

                if (fromHeader != null && dateHeader != null)
                {
                    string sender = fromHeader.Value; // Extract sender
                    DateTime date = DateTime.Parse(dateHeader.Value); // Parse email date
                    string subject = headers.FirstOrDefault(h => h.Name == "Subject")?.Value ?? "(No Subject)";
                    senders.Add((sender, subject, date));
                }
            }

            // Order by date descending and return distinct senders
            return senders
                .OrderByDescending(s => s.Date)
                .GroupBy(s => s.Sender)
                .Select(g => g.First())
                .ToList();
        }

        // Mark email as read
        public async Task MarkAsReadAsync(GmailService service, string messageId)
        {
            var modifyRequest = new ModifyMessageRequest
            {
                RemoveLabelIds = new[] { "UNREAD" } // Remove UNREAD label
            };

            await service.Users.Messages.Modify(modifyRequest, "me", messageId).ExecuteAsync();
            Console.WriteLine($"Message {messageId} marked as read.");
        }

        // Archive email
        public async Task ArchiveEmailAsync(GmailService service, string messageId)
        {
            var modifyRequest = new ModifyMessageRequest
            {
                RemoveLabelIds = new[] { "INBOX" } // Remove INBOX label
            };

            await service.Users.Messages.Modify(modifyRequest, "me", messageId).ExecuteAsync();
            Console.WriteLine($"Message {messageId} archived.");
        }

        // Get inbox emails
        public async Task<IList<Message>> GetInboxEmails()
        {
            var request = _gmailService.Users.Messages.List("me");
            request.LabelIds = new List<string> { "INBOX" };
            var response = await request.ExecuteAsync();
            return response.Messages;
        }

        // Get sent emails
        public async Task<IList<Message>> GetSentEmails()
        {
            var request = _gmailService.Users.Messages.List("me");
            request.LabelIds = new List<string> { "SENT" };
            var response = await request.ExecuteAsync();
            return response.Messages;
        }

        // Get archived emails (not in inbox)
        public async Task<IList<Message>> GetArchivedEmails()
        {
            var request = _gmailService.Users.Messages.List("me");
            request.Q = "-in:inbox";
            var response = await request.ExecuteAsync();
            return response.Messages;
        }
    }
}
