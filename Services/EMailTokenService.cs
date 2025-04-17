
using Google.Apis.Auth.OAuth2;
using Google.Apis.Auth.OAuth2.Flows;
using Google.Apis.Auth.OAuth2.Responses;
using GoogleLogin.Models;
using Microsoft.AspNetCore.SignalR;
using Newtonsoft.Json;
using System.Data;
using System.Net.Http.Headers;

namespace GoogleLogin.Services
{
    public class EMailTokenService
    {
        private readonly IServiceScopeFactory       _serviceScopeFactory;
		private readonly IHubContext<DataWebsocket> _hubContext;
        private readonly ILogger<EMailService>      _logger;
        private readonly IConfiguration             _configuration;
        public static readonly string[]             Scopes = { "email", "profile", "https://www.googleapis.com/auth/gmail.modify" };
        private static readonly HttpClient          _httpClient = new HttpClient();

        public EMailTokenService(
            IServiceScopeFactory serviceScopeFactory, 
            IHubContext<DataWebsocket> dataWebsocket, 
            IConfiguration configuratoin,
            ILogger<EMailService> logger)
		{
            _serviceScopeFactory    = serviceScopeFactory;
            _hubContext             = dataWebsocket;
            _configuration          = configuratoin;
            _logger                 = logger;
        }

        public void SaveToken(string strMailName, TokenResponse tokenResponse)
        {
            using (var scope = _serviceScopeFactory.CreateScope())
            {
                var _dbContext = scope.ServiceProvider.GetRequiredService<AppIdentityDbContext>();
                var _item = _dbContext.TbMailAccount
                    .Where(item => item.mail == strMailName)
                    .FirstOrDefault();

                if (_item != null)
                {
                    _item.accessToken  = tokenResponse.AccessToken;
                    _item.refreshToken = tokenResponse.RefreshToken;
                    _dbContext.SaveChanges();
                }
            }
        }

        public string GetAccessTokenFromMailName(string mailName)
        {
            string accessToken = string.Empty;

            using (var scope = _serviceScopeFactory.CreateScope())
            {
                var _dbContext = scope.ServiceProvider.GetRequiredService<AppIdentityDbContext>();
                var _item = _dbContext.TbMailAccount
                        .Where(item => item.mail == mailName)
                        .FirstOrDefault();

                if ( _item != null )
                {
                    accessToken = _item.accessToken ?? string.Empty;
                }
            }

            return accessToken;
        }

        public string GetRefreshTokenFromMailName( string mailName )
        {
            string refreshToken = string.Empty;

            using (var scope = _serviceScopeFactory.CreateScope() )
            {
                var _dbContext = scope.ServiceProvider.GetRequiredService<AppIdentityDbContext>();
                var _item = _dbContext.TbMailAccount
                        .Where(item => item.mail == mailName)
                        .FirstOrDefault();

                if ( _item != null )
                {
                    refreshToken = _item.refreshToken;
                }
            }
            return refreshToken;
        }

        public async Task<bool> RefreshTokenAync(string strMailName, string strUserId)
        {
            string refreshToken = string.Empty;

            using (var scope = _serviceScopeFactory.CreateScope())
            {
                var _dbContext = scope.ServiceProvider.GetRequiredService<AppIdentityDbContext>();
                var _item = _dbContext.TbMailAccount
                    .Where(item => item.mail == strMailName && item.userId == strUserId )
                    .FirstOrDefault();

                if (_item != null)
                {
                    refreshToken    = _item.refreshToken;

                    if (string.IsNullOrEmpty(refreshToken)) return false;

                    var flow = new GoogleAuthorizationCodeFlow(
                        new GoogleAuthorizationCodeFlow.Initializer
                        {
                            ClientSecrets = new ClientSecrets
                            {
                                ClientId        = _configuration["clientId"],
                                ClientSecret    = _configuration["clientSecret"]
                            },
                            Scopes = Scopes
                        });

                    try
                    {  
                        TokenResponse tokenResponse = await flow.RefreshTokenAsync(
                            userId: string.Empty,
                            refreshToken: refreshToken,
                            CancellationToken.None);

                        _item.accessToken = tokenResponse.AccessToken;
                        _item.refreshToken = refreshToken;
                        _dbContext.SaveChanges();

                        return true;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning($"Error refreshing token: {ex.Message}");
                        return false;
                    }
                }
                return false;
            }
        }

        public async Task<bool> RefreshTokenAync(int nMailIdx)
        {
            string refreshToken = string.Empty;

            using (var scope = _serviceScopeFactory.CreateScope())
            {
                var _dbContext = scope.ServiceProvider.GetRequiredService<AppIdentityDbContext>();
                var _item = _dbContext.TbMailAccount
                    .Where(item => item.id == nMailIdx )
                    .FirstOrDefault();

                if (_item != null)
                {
                    refreshToken    = _item.refreshToken;

                    if (string.IsNullOrEmpty(refreshToken)) return false;

                    var flow = new GoogleAuthorizationCodeFlow(
                        new GoogleAuthorizationCodeFlow.Initializer
                        {
                            ClientSecrets = new ClientSecrets
                            {
                                ClientId        = _configuration["clientId"],
                                ClientSecret    = _configuration["clientSecret"]
                            },
                            Scopes = Scopes
                        });

                    try
                    {  
                        TokenResponse tokenResponse = await flow.RefreshTokenAsync(
                            userId: string.Empty,
                            refreshToken: refreshToken,
                            CancellationToken.None);

                        _item.accessToken = tokenResponse.AccessToken;
                        _item.refreshToken = refreshToken;
                        _dbContext.SaveChanges();

                        return true;
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error refreshing token: {ex.Message}");
                        return false;
                    }
                }
                return false;
            }
        }

        public async Task<string> GetGmailNameAsync(string accessToken)
        {
            string UserInfoEndpoint = "https://www.googleapis.com/oauth2/v3/userinfo";

            using (var client = new HttpClient())
            {
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

                var response = await client.GetAsync(UserInfoEndpoint);
                response.EnsureSuccessStatusCode(); 

                var content = await response.Content.ReadAsStringAsync();

                var userInfo = JsonConvert.DeserializeObject<GoogleUserInfo>(content) ?? null;

                if (userInfo != null) return userInfo.Email;
                else return string.Empty;
            }
        }

        public async Task<bool> IsAccessTokenExpired(string accessToken)
        {
            var requestUri = $"https://www.googleapis.com/oauth2/v3/tokeninfo?access_token={accessToken}";

            try
            {
                var response = await _httpClient.GetStringAsync(requestUri);
                var tokenInfo = JsonConvert.DeserializeObject<TokenResponse>(response);

                return false;  // Token is not expired
            }
            catch (HttpRequestException)
            {
                return true;  // Token is expired or invalid
            }
        }

        public List<TbMailAccount> GetMailAccountList(string userId = "")
        {
            List<TbMailAccount> mailAccountList = new List<TbMailAccount>();

            try
            {
                using (var scope = _serviceScopeFactory.CreateScope())
                {
                    var dbContext = scope.ServiceProvider.GetRequiredService<AppIdentityDbContext>();

                    mailAccountList = dbContext.TbMailAccount
                        .Where(e => e.userId == userId)
                        .ToList();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error retrieving email list for user {userId}: {ex.Message}");
            }

            return mailAccountList;
        }
    }
}
