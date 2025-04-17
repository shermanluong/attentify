using Azure.Core;
using Google.Apis.Auth.OAuth2;
using ShopifySharp.GraphQL;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace GoogleLogin.Services
{
	public class ShopifyAuthHelper
	{
		private readonly string _clientId;
		private readonly string _redirectUri;
		private readonly string _clientSecret;

		public ShopifyAuthHelper(string clientId, string clientSecret, string redirectUri = "https://localhost:7150/shopify/callback")
		{
			_clientId = clientId;
			_redirectUri = redirectUri;
			_clientSecret = clientSecret;
		}

		public string BuildAuthorizationUrl(string shop, string strRedirectUrl = null, string[] scopes = null)
		{
			if (scopes == null)
			{
				scopes = new string[] { "read_orders", "write_orders", "read_customers"};
			}
			if (string.IsNullOrEmpty(strRedirectUrl))
			{
				strRedirectUrl = _redirectUri;
			}
			var scope = string.Join(",", scopes);
			return $"https://{shop}/admin/oauth/authorize?" +
				   $"client_id={_clientId}" +
				   $"&scope={Uri.EscapeDataString(scope)}" +
				   $"&redirect_uri={Uri.EscapeDataString(strRedirectUrl)}";
		}


		public async Task<string> ExchangeCodeForAccessToken(string shop, string code)
		{
			var tokenRequestUrl = $"https://{shop}/admin/oauth/access_token";
			var client = new HttpClient();

			var requestBody = new
			{
				client_id = _clientId,
				client_secret = _clientSecret,
				code = code
			};

			var response = await client.PostAsync(
				tokenRequestUrl,
				new StringContent(JsonSerializer.Serialize(requestBody), System.Text.Encoding.UTF8, "application/json")
			);

			response.EnsureSuccessStatusCode();

			var responseBody = await response.Content.ReadAsStringAsync();
			var tokenResponse = JsonSerializer.Deserialize<AccessTokenResponse>(responseBody);

			return tokenResponse.AccessToken;
		}		
	}

	public class AccessTokenResponse
	{
		[JsonPropertyName("access_token")]
		public string AccessToken { get; set; }

		[JsonPropertyName("scope")]
		public string Scope { get; set; }
	}

}
