
using Anthropic.SDK;
using Anthropic.SDK.Constants;
using Anthropic.SDK.Messaging;
using Google.Cloud.PubSub.V1;
using System.Collections.Generic;
using System.Data;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace GoogleLogin.Services
{
    public class LLMService
    {
        private readonly ILogger<LLMService>    _logger;
        private readonly IConfiguration         _configuration;
        private static   AnthropicClient?       _llmServer;
        private string                          _strQuery;
        public LLMService(
            ILogger<LLMService> logger,
            IConfiguration      configuration)
        {
            _logger         =    logger;
            _configuration  =    configuration;
            _llmServer      =    new AnthropicClient(_configuration["AnthropicAPIKey"]);
            _strQuery = $"The following text is an order, cancellation, or refund email encoded in Base64 from a Shopify customer. " +
                    $"Please check if the order ID field exists and is correct. If the email is correct, then output the necessary string formatted as JSON. " +
                    $"The JSON string should include order_id, type (either cancel or refund), status (1 if correct, otherwise 0), and msg " +
                    $"(a message requesting the order ID if the email is incorrect; if the email is correct, msg should be null). " +
                    $" I need only the JSON output: ";
        }

        public async Task<string> GetResponseLLM(string strBody)
        {
            if (_llmServer == null) return string.Empty;
            try
            {
                string strText = _strQuery + strBody;

                var messages = new List<Message>()
                {
                    new Message(RoleType.User, strText)
                };

                var parameters = new MessageParameters()
                {
                    Messages    = messages,
                    MaxTokens   = 2048,
                    Model       = AnthropicModels.Claude35Sonnet,
                    Stream      = false,
                    Temperature = 1.0m,
                };
                var finalResult = await _llmServer.Messages.GetClaudeMessageAsync(parameters);
                return finalResult.Message.ToString();
            }
            catch (Exception ex)
            {
                Console.WriteLine("llmservice/getresponseasync" + ex.ToString());
            }
            return string.Empty;
        }

        public async Task<string> GetResponseAsync(string strBody, string strUserName, EMailService _emailService = null)
        {
            if (_llmServer == null) return string.Empty;
            try
            {
                string strOcrText = await ExtractTextOcr(_emailService, strBody);
                if(!string.IsNullOrEmpty(strOcrText))
                {
                    strBody = $"{strBody}\n{strOcrText}";
                }

                string strTxt = _strQuery + strBody;
                    
                var messages = new List<Message>()
                {
                    new Message(RoleType.User, strTxt) 
                };
            
                var parameters = new MessageParameters()
                {
                    Messages = messages,                
                    MaxTokens = 2048,
                    Model = AnthropicModels.Claude35Sonnet,
                    Stream = false,
                    Temperature = 1.0m,
                };
                var finalResult = await _llmServer.Messages.GetClaudeMessageAsync(parameters);

                return finalResult.Message.ToString();
            }catch(Exception ex)
            {
                Console.WriteLine("llmservice/getresponseasync" + ex.ToString());
            }
            return string.Empty;
        }

        public async Task<string> GetResponseAsyncOnlyText(string strBody)
        {
            if (_llmServer == null) return string.Empty;

            try
            {
                string strText = _strQuery + strBody;

                var messages = new List<Message>()
                { 
                    new Message(RoleType.User, strText)
                };

                var parameters = new MessageParameters()
                {
                    Messages = messages,
                    MaxTokens = 2048,
                    Model = AnthropicModels.Claude35Sonnet,
                    Stream = false,
                    Temperature = 1.0m,
                };
                var finalResult = await _llmServer.Messages.GetClaudeMessageAsync(parameters);
                return finalResult.Message.ToString();
            }
            catch (Exception ex)
            {
                Console.WriteLine("llmservice/getresponseasync" + ex.ToString());
            }
            return string.Empty;
        }

        public async Task<string> GetResponseAsync(string strBody)
        {
            if (_llmServer == null) return string.Empty;
            try
            {
                string strTxt = _strQuery + strBody;
               
                var messages = new List<Message>()
                {                    
                    new Message(RoleType.User, strTxt)
                };

                var parameters = new MessageParameters()
                {
                    Messages = messages,
                    MaxTokens = 2048,
                    Model = AnthropicModels.Claude35Sonnet,
                    Stream = false,
                    Temperature = 1.0m,
                };
                var finalResult = await _llmServer.Messages.GetClaudeMessageAsync(parameters);
                
                Console.WriteLine(finalResult.Message.ToString());
                return finalResult.Message.ToString();
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.ToString());
            }
            return string.Empty;
        }

        private async Task<string> ExtractTextOcr(EMailService emailservice, string strBody)
        {
            string _strBody = emailservice.GetMailBodyAsHtml(strBody);
			var imgMatch = Regex.Match(_strBody, @"<img[^>]*src=""data:image\/[a-zA-Z]+;base64,([^""]+)""");

			if (imgMatch.Success)
			{
				var base64ImageData = Convert.FromBase64String(imgMatch.Groups[1].Value);
                var strExtractText = OcrService.ExtractTextFromImage(base64ImageData);
                return strExtractText;
            }
            else
            {
				var imgUrlMatches = Regex.Matches(_strBody, @"<img[^>]*src=""(http[s]?:\/\/[^\s""]+)""");
                List<string> lstUrls = imgUrlMatches.Cast<Match>().Select(m => m.Groups[1].Value).ToList();
                string strResult = "";
                foreach(var imgUrlMatch in lstUrls)
                {
					HttpClient HttpClient = new HttpClient();
					var imageBytes = await HttpClient.GetByteArrayAsync(imgUrlMatch);

                    string strExtrace = OcrService.ExtractTextFromImage(imageBytes);
                    if (string.IsNullOrWhiteSpace(strExtrace)) continue;
                    strResult = $"{OcrService.ExtractTextFromImage(imageBytes)}\n{strResult}";
				}
                return strResult;
			}
		}
    }

}
