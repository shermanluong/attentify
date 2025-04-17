using GoogleLogin.Models;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using ShopifySharp;
using System.Text;
using System.Text.Json;

namespace GoogleLogin.Services
{
    public class ShopifyService
    {
        private readonly IServiceScopeFactory _serviceScopeFactory;
        public static readonly Dictionary<string, int> _mapPaymentStatus = new Dictionary<string, int>() { { "paid", 0 }, { "partially_refunded", 1 }, { "refunded", 2 } };
        public static readonly Dictionary<int, string> mapPaymentStatus = _mapPaymentStatus.ToDictionary(
            kvp => kvp.Value, 
            kvp => kvp.Key    
        );
        
        public static readonly Dictionary<string, int> _mapFulfiilmentStatus = new Dictionary<string, int>() { { "null", 0 }, { "fulfilled", 1} };
        public static readonly Dictionary<int, string> mapFulfiilmentStatus = new Dictionary<int, string>() { { 0, "Unfulfilled"}, {1, "Fullfilled"} };

        private readonly string _domain;
        private readonly string _apiVersion;
        private readonly ILogger<ShopifyService> _logger;
        //private readonly DataWebsocket _dataWebsocket;
		private readonly IHubContext<DataWebsocket> _hubContext;
		public ShopifyService(
            IServiceScopeFactory serviceScopeFactory, 
            IConfiguration configuration, 
            ILogger<ShopifyService> logger, 
            IHubContext<DataWebsocket> dataWebsocket)
        {
            _serviceScopeFactory    = serviceScopeFactory;
            _logger                 = logger;
            _domain                 = configuration["Domain"];
            _apiVersion             = configuration["ApiVersion"];
			_hubContext  = dataWebsocket;
        }

        public async Task<bool> CancelOrder(string strOrderNum)
        {
            strOrderNum = AddPrefixIfMissing(strOrderNum);
            KeyValuePair<string, string> p = GetAccessTokenByOrder(strOrderNum);
            if(p.Key == null || p.Value == null) return false;

			string cancelUrl = $"https://{p.Value}/admin/api/{_apiVersion}/orders/{p.Key}/cancel.json";
            using (HttpClient client = new HttpClient())
            {
                client.DefaultRequestHeaders.Add("X-Shopify-Access-Token", p.Key);

                try
                {
                    //HttpResponseMessage response = await client.GetAsync(urlOrders);

                    //if (response.IsSuccessStatusCode)
                    //{
                    //    string responseData = await response.Content.ReadAsStringAsync();
                    //    var orders = JsonDocument.Parse(responseData).RootElement.GetProperty("orders");

                    //    foreach (var order in orders.EnumerateArray())
                    //    {
                    //        string orderName = order.GetProperty("name").GetString();
                    //        if (orderName == strOrderNum)
                    //        {
                    //            targetOrderId = order.GetProperty("id").GetInt64();
                    //            break;
                    //        }
                    //    }

                    //    if (targetOrderId == null)
                    //    {
                    //        Console.WriteLine($"Order with name {strOrderNum} not found.");
                    //        return false;
                    //    }
                    //}
                    //else
                    //{
                    //    Console.WriteLine($"Error fetching orders: {response.StatusCode} - {response.ReasonPhrase}");
                    //    return false;
                    //}

                    var cancelOptions = new OrderCancelOptions
                    {
                        Restock = true,
                        Reason = "customer",
                        SendCancellationReceipt = true
                    };
                    var jsonContent = new StringContent(
                       JsonSerializer.Serialize(cancelOptions),
                       Encoding.UTF8,
                       "application/json"
                    );
                    HttpResponseMessage cancelResponse = await client.PostAsync(cancelUrl, jsonContent);

                    if (cancelResponse.IsSuccessStatusCode)
                    {
                        string cancelData = await cancelResponse.Content.ReadAsStringAsync();
                        Console.WriteLine("Order canceled successfully:");
                        _logger.LogInformation("Order canceled successfully:");
                        return true;
                    }
                    else
                    {
                        string cancelError = await cancelResponse.Content.ReadAsStringAsync();
                        Console.WriteLine($"Error canceling order: {cancelResponse.StatusCode} - {cancelResponse.ReasonPhrase}");
                        Console.WriteLine($"Error Details: {cancelError}");
                        _logger.LogInformation($"Error canceling order: {cancelResponse.StatusCode} - {cancelResponse.ReasonPhrase}");
                        _logger.LogInformation($"Error Details: {cancelError}");
                        return false;
                    }
                }
                catch (Exception ex)
                {
                    //Console.WriteLine($"Exception: {ex.Message}");
                    _logger.LogError(ex, ex.Message);
                }
                return false;
            }
        }

		public async Task<bool> CancelOrder(long nOrderId)
		{
			KeyValuePair<string, string> p = GetAccessTokenByOrder(nOrderId);
			if (p.Key == null || p.Value == null) return false;

			string cancelUrl = $"https://{p.Value}/admin/api/{_apiVersion}/orders/{nOrderId}/cancel.json";

			using (HttpClient client = new HttpClient())
			{
				client.DefaultRequestHeaders.Add("X-Shopify-Access-Token", p.Key);

				try
				{
					var cancelOptions = new OrderCancelOptions
					{
						//Restock = true,
						Reason = "customer",
						//SendCancellationReceipt = true
					};
					
                    var jsonContent = new StringContent(
					   JsonSerializer.Serialize(cancelOptions),
					   Encoding.UTF8,
					   "application/json"
					);
					HttpResponseMessage cancelResponse = await client.PostAsync(cancelUrl, jsonContent);

					if (cancelResponse.IsSuccessStatusCode)
					{
						string responseDetail = await cancelResponse.Content.ReadAsStringAsync();
						_logger.LogInformation("Order canceled successfully:" + responseDetail);
						return true;
					}
					else
					{
						string cancelError = await cancelResponse.Content.ReadAsStringAsync();
						JsonDocument doc = JsonDocument.Parse(cancelError);
                        string strError = doc.RootElement.GetProperty("error").ToString();
						_logger.LogInformation($"Error Details: {strError}");
						return false;
					}
				}
				catch (Exception ex)
				{
					_logger.LogError(ex, ex.Message);
				}
				return false;
			}
		}

        public async Task<bool> RefundOrder(long nOrderId)
        {
            KeyValuePair<string, string> p = GetAccessTokenByOrder(nOrderId);
            if (p.Key == null || p.Value == null) return false;

            string calculateRefundUrl = $"https://{p.Value}/admin/api/{_apiVersion}/orders/{nOrderId}/refunds/calculate.json";
            string refundUrl = $"https://{p.Value}/admin/api/{_apiVersion}/orders/{nOrderId}/refunds.json";
            _logger.LogInformation(calculateRefundUrl);
            _logger.LogInformation(refundUrl);
            using (HttpClient client = new HttpClient())
            {
                client.DefaultRequestHeaders.Add("X-Shopify-Access-Token", p.Key);

                try
                {
                    // First calculate the refund preview
                    
                    var calculateData = new
                    {
                        refund = new
                        {
                            currency = "CAD",
                            shipping = new {
                                amount = 1.0
                            }
                        }
                    };

                    var jsonCalculateData = new StringContent(
                        JsonSerializer.Serialize(calculateData),
                        Encoding.UTF8,
                        "application/json"
                    );

                    HttpResponseMessage previewResponse = await client.PostAsync(calculateRefundUrl, jsonCalculateData);

                    if (!previewResponse.IsSuccessStatusCode)
                    {
                        _logger.LogError($"Failed to calculate refund for order {nOrderId}");
                        return false;
                    }

                    string previewContent = await previewResponse.Content.ReadAsStringAsync();
                    var previewJson = JsonDocument.Parse(previewContent);
                    var refundObj = previewJson.RootElement.GetProperty("refund");

                    // Send refund request using the calculated refund object
                    var refundData = new
                    {
                        refund = new
                        {
                            shipping = refundObj.GetProperty("shipping"),
                            refund_line_items = refundObj.GetProperty("refund_line_items"),
                            transactions = refundObj.GetProperty("transactions"),
                            quantity = 1,
                            notify = true,
                            source = "external"
                        }
                    };

                    var jsonRefundContent = new StringContent(
                        JsonSerializer.Serialize(refundData),
                        Encoding.UTF8,
                        "application/json"
                    );

                    HttpResponseMessage refundResponse = await client.PostAsync(refundUrl, jsonRefundContent);
                    if (refundResponse.IsSuccessStatusCode)
                    {
                        string responseDetail = await refundResponse.Content.ReadAsStringAsync();
                        _logger.LogInformation("Order refunded successfully: " + responseDetail);
                        return true;
                    }
                    else
                    {
                        string refundError = await refundResponse.Content.ReadAsStringAsync();
                        _logger.LogError($"Refund failed for order {nOrderId}: {refundError}");
                        return false;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, ex.Message);
                }
                return false;
            }
        }

		/*public async Task<bool> RefundOrder(long nOrderId)
		{
            KeyValuePair<string, string> p = GetAccessTokenByOrder(nOrderId);
			if (p.Key == null || p.Value == null) return false;

            _logger.LogInformation($"Shopfiy access key when refun is {p.Key}");
			var service = new OrderService($"https://{p.Value}", p.Key);

			try
			{
				await service.CancelAsync(nOrderId);
				await service.CancelAsync(nOrderId, new OrderCancelOptions
				{
				    Restock = true,
				    Reason = "customer",
				    SendCancellationReceipt = true
				});

				Console.WriteLine($"Order {nOrderId} canceled successfully!");
				_logger.LogInformation($"Order {nOrderId} canceled successfully!");
				return true;
			}
			catch (ShopifyException ex)
			{
				Console.WriteLine($"Error canceling order: {ex.Message}");
				_logger.LogError(ex, ex.Message);
				return false;
			}
		}*/

		static string AddPrefixIfMissing(string input)
        {
            if (!input.StartsWith("#"))
            {
                return "#" + input; 
            }
            return input; 
        }

        public async Task SaveAccessToken(string strUsreId, string strShop, string strToken)
        {
            using (var scope = _serviceScopeFactory.CreateScope()) 
            {
                var _dbContext = scope.ServiceProvider.GetRequiredService<AppIdentityDbContext>();

                try
                {
                    var _one = 
                        await _dbContext
                        .TbTokens
                        .Where(e => e.ShopDomain == strShop && e.UserId == strUsreId)
                        .FirstOrDefaultAsync();
                    if (_one == null)
                    {
                        _dbContext.Add(new TbShopifyToken
                        {
                            UserId      = strUsreId,
                            ShopDomain  = strShop,
                            AccessToken = strToken,
                            DateCreated = DateTime.Now,
                            DateUpdated = DateTime.Now,
                        });
                    }
                    else
                    {
                        _one.DateUpdated = DateTime.Now;
                        _one.AccessToken = strToken;

                    }
                    await _dbContext.SaveChangesAsync();
                    _ = Task.Run(async () => await OrderRequest(strShop, strToken));
                }
                catch(Exception ex)
                {
                    Console.WriteLine("shopify/saveaccesstoken " + ex.StackTrace);
                }
            }
        }
    
        public async void OrderRequest()
        {
            using (var scope = _serviceScopeFactory.CreateScope())
            {
                var _dbContext = scope.ServiceProvider.GetRequiredService<AppIdentityDbContext>();
                foreach(var objToken in _dbContext.TbTokens)
                {
                    if(objToken == null)continue;
                    if (string.IsNullOrEmpty(objToken.ShopDomain) || string.IsNullOrEmpty(objToken.AccessToken)) continue;
                    await OrderRequest(objToken.ShopDomain, objToken.AccessToken);
                }
            }
        }

        public async Task<TbOrder> OrderRequest(long orderId)
        {
            using (var scope = _serviceScopeFactory.CreateScope()) 
            {
                var _dbContext = scope.ServiceProvider.GetRequiredService<AppIdentityDbContext>();
                TbOrder p = await _dbContext.TbOrders.Where(e => e.or_id == orderId).FirstOrDefaultAsync();
                if (p == null) return null;
                TbShopifyToken _p = await _dbContext.TbTokens.Where(e => e.ShopDomain == p.or_owner).FirstOrDefaultAsync();
                if(_p == null) return null;
                string strShop = _p.ShopDomain;

                string urlOrders = $"https://{p.or_owner}/admin/api/{_apiVersion}/orders/{orderId}.json";

                using (HttpClient client = new HttpClient())
                {
                    client.DefaultRequestHeaders.Add("X-Shopify-Access-Token", _p.AccessToken);

                    try
                    {
                        HttpResponseMessage response = await client.GetAsync(urlOrders);

                        if (response.IsSuccessStatusCode)
                        {
                            string responseData = await response.Content.ReadAsStringAsync();
                            var order = JsonDocument.Parse(responseData).RootElement.GetProperty("order");
                            
                            {
                                try
                                {
                                    var customerElement = order.GetProperty("customer");
                                    var line_items = order.GetProperty("line_items");
                                    int nCnt = 0;
                                    if (line_items.ValueKind == JsonValueKind.Array)
                                    {
                                        foreach (var line_item in line_items.EnumerateArray())
                                        {
                                            nCnt += Convert.ToInt32(line_item.GetProperty("quantity").ToString());
                                        }
                                    }
                                    string strPaymentStatus = order.GetProperty("financial_status").ToString();
                                    string strFulfillStatus = order.TryGetProperty("fulfillment_status", out var fulfillmentStatusElement)
                                        ? (fulfillmentStatusElement.ValueKind == JsonValueKind.Null ? "null" : fulfillmentStatusElement.GetString())
                                        : "null";

                                    long or_id = Convert.ToInt64(order.GetProperty("id").ToString());
                                    string or_name = order.GetProperty("name").ToString() ?? "";
                                    string or_channel = "online store";
                                    string or_customer = customerElement.GetProperty("email").ToString();
                                    float or_total = Convert.ToSingle(order.GetProperty("total_price").ToString());
                                    int or_itemCnt = nCnt;
                                    string or_owner = strShop;
                                    int or_payment_status = _mapPaymentStatus[strPaymentStatus];
                                    int or_fulfill_status = _mapFulfiilmentStatus[strFulfillStatus];
                                    var dateString = order.GetProperty("created_at").ToString();
                                    dateString = dateString.Replace(" ", "");
                                    DateTime or_date = DateTime.Parse(dateString);
                                    int or_status = 0;
                                    if (order.TryGetProperty("cancel_reason", out JsonElement cancelReason))
                                    {
                                        if (cancelReason.ValueKind != JsonValueKind.Null)
                                        {
                                            if (order.TryGetProperty("cancelled_at", out JsonElement cancelDate))
                                            {
                                                if (cancelDate.ValueKind != JsonValueKind.Null)
                                                {
                                                    or_status = 2;
                                                }
                                            }
                                        }
                                    }
                                    TbOrder pOrder = new TbOrder
                                    {
                                        or_id = or_id,
                                        or_name = or_name,
                                        or_channel = or_channel,
                                        or_customer = or_customer,
                                        or_total = or_total,
                                        or_itemCnt = or_itemCnt,
                                        or_owner = or_owner,
                                        or_payment_status = or_payment_status,
                                        or_fulfill_status = or_fulfill_status,
                                        or_date = or_date,
                                    };
                                    TbOrder _pOrigin = await _dbContext.TbOrders.Where(e => e.or_id == pOrder.or_id).FirstOrDefaultAsync();
                                    if (_pOrigin == null)
                                    {
                                        _dbContext.TbOrders.Add(pOrder);
                                    }
                                    else
                                    {
                                        _pOrigin.or_customer = or_customer;
                                        _pOrigin.or_date = or_date;
                                        _pOrigin.or_status = or_status;
                                        _pOrigin.or_fulfill_status = or_fulfill_status;
                                        _pOrigin.or_payment_status = or_payment_status;
                                    }
                                    await _dbContext.SaveChangesAsync();

                                    return _pOrigin;
                                }
                                catch (Exception _ex)
                                {
                                    Console.WriteLine(_ex.Message);
                                    _logger.LogError(_ex, _ex.Message);
                                }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine(ex.Message);
                        _logger.LogError(ex, ex.Message);
                    }
                }                
            }
            return null;
            
        }

        public async Task<string> GetOrderInfoRequest(long orderId)
        {
            using (var scope = _serviceScopeFactory.CreateScope()) 
            {
                var _dbContext = scope.ServiceProvider.GetRequiredService<AppIdentityDbContext>();
                TbOrder p = await _dbContext.TbOrders.Where(e => e.or_id == orderId).FirstOrDefaultAsync();
                
                if (p == null) return null;
                TbShopifyToken _p = await _dbContext.TbTokens.Where(e => e.ShopDomain == p.or_owner).FirstOrDefaultAsync();
                if (_p == null) return null;
                string strShop = _p.ShopDomain;
                
                string urlOrders = $"https://{strShop}/admin/api/{_apiVersion}/orders/{orderId}.json";
                _logger.LogInformation(urlOrders);
                using (HttpClient client = new HttpClient())
                {
                    client.DefaultRequestHeaders.Add("X-Shopify-Access-Token", _p.AccessToken);
                    //_logger.LogInformation(_p.AccessToken);
                    try
                    {
                        HttpResponseMessage response = await client.GetAsync(urlOrders);
                        string responseData1 = await response.Content.ReadAsStringAsync();
                        _logger.LogInformation(responseData1);
                        _logger.LogInformation("shopify step 1 ....");
                        if (response.IsSuccessStatusCode)
                        {
                            _logger.LogInformation("shopify step 2 ....");
                            string responseData = await response.Content.ReadAsStringAsync();
                            return responseData;
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine(ex.Message);
                        _logger.LogError(ex, ex.Message);
                    }
                }
            }
            return "";
        }

        public async Task OrderRequest(string strShop, string strToken)
        {
            string urlOrders = $"https://{strShop}/admin/api/{_apiVersion}/orders.json?status=any&limit=150";

            using (HttpClient client = new HttpClient())
            {
                client.DefaultRequestHeaders.Add("X-Shopify-Access-Token", strToken);
                _logger.LogInformation("Order Request is preparing");
                _logger.LogInformation($"Order Request url is {urlOrders}");
                try
                {
                    HttpResponseMessage response = await client.GetAsync(urlOrders);
                    _logger.LogInformation(await response.Content.ReadAsStringAsync());
                    if (response.IsSuccessStatusCode)
                    {
                        Console.WriteLine("OK");
                        string responseData = await response.Content.ReadAsStringAsync();
                        var orders = JsonDocument.Parse(responseData).RootElement.GetProperty("orders");
                        Console.WriteLine(orders);
                        using (var scope = _serviceScopeFactory.CreateScope()) 
                        {
                            var _dbContext = scope.ServiceProvider.GetRequiredService<AppIdentityDbContext>();

                            foreach (var order in orders.EnumerateArray())
                            {
                                try
                                {
                                    var customerElement = order.GetProperty("customer");
                                    var line_items = order.GetProperty("line_items");
                                    int nCnt = 0;
                                    if (line_items.ValueKind == JsonValueKind.Array)
                                    {
                                        foreach (var line_item in line_items.EnumerateArray())
                                        {
                                            nCnt += Convert.ToInt32(line_item.GetProperty("quantity").ToString());                                            
                                        }
                                    }
                                    string strPaymentStatus = order.GetProperty("financial_status").ToString();
                                    string strFulfillStatus = order.TryGetProperty("fulfillment_status", out var fulfillmentStatusElement)
                                        ? (fulfillmentStatusElement.ValueKind == JsonValueKind.Null ? "null" : fulfillmentStatusElement.GetString())
                                        : "null";

                                    long or_id = Convert.ToInt64(order.GetProperty("id").ToString());
                                    string or_name = order.GetProperty("name").ToString() ?? "";
                                    string or_channel = "online store";//order.GetProperty("channel").ToString(),
                                    string or_customer = customerElement.GetProperty("email").ToString();
                                    string or_customer_name = $"{customerElement.GetProperty("first_name").ToString()} {customerElement.GetProperty("last_name").ToString()}";
                                    string or_phone = customerElement.GetProperty("phone").ToString();
                                    if (string.IsNullOrEmpty(or_phone))
                                    {
                                        var defaultAddress = customerElement.GetProperty("default_address");
                                        if(defaultAddress.ValueKind != JsonValueKind.Null)
                                        {
                                            or_phone = defaultAddress.GetProperty("phone").ToString();
                                        }
                                    }
                                    float or_total = Convert.ToSingle(order.GetProperty("total_price").ToString());
                                    int or_itemCnt = nCnt;
                                    string or_owner = strShop;
                                    int or_payment_status = _mapPaymentStatus[strPaymentStatus];
                                    int or_fulfill_status = _mapFulfiilmentStatus[strFulfillStatus];
                                    var dateString = order.GetProperty("created_at").ToString();
                                    dateString = dateString.Replace(" ", "");
                                    DateTime or_date = DateTime.Parse(dateString);
                                    int or_status = 0;                                    
                                    if (order.TryGetProperty("cancel_reason", out JsonElement cancelReason))
                                    {
                                        if(cancelReason.ValueKind != JsonValueKind.Null)
                                        {
                                            if(order.TryGetProperty("cancelled_at", out JsonElement cancelDate))
                                            {
                                                if(cancelDate.ValueKind != JsonValueKind.Null)
                                                {
                                                    or_status = 2;
                                                }
                                            }
                                        }
                                    }
                                    TbOrder pOrder = new TbOrder
                                    {
                                        or_id = or_id,
                                        or_name = or_name,
                                        or_channel = or_channel,
                                        or_customer = or_customer,
                                        or_total    = or_total,
                                        or_itemCnt = or_itemCnt,
                                        or_owner = or_owner,
                                        or_payment_status= or_payment_status,
                                        or_fulfill_status= or_fulfill_status,
                                        or_date = or_date,
                                        or_customer_name = or_customer_name,
                                        or_phone = or_phone,
                                    };

                                    TbOrder _pOrigin = await _dbContext.TbOrders.Where(e => e.or_id == pOrder.or_id).FirstOrDefaultAsync();
                                    Console.WriteLine(or_id);
                                    if (_pOrigin == null)
                                    {
                                        _dbContext.TbOrders.Add(pOrder);
                                    }
                                    else
                                    {
                                        _pOrigin.or_customer = or_customer;
                                        _pOrigin.or_date = or_date;
                                        _pOrigin.or_status = or_status;
                                        _pOrigin.or_fulfill_status = or_fulfill_status;
                                        _pOrigin.or_payment_status = or_payment_status;
                                        _pOrigin.or_phone = or_phone;
                                        _pOrigin.or_customer_name = or_customer_name;
                                    }
                                    int nRes = await _dbContext.SaveChangesAsync();
                                    Console.WriteLine(nRes);
                                }catch(Exception _ex) {
                                    Console.WriteLine("shopifyservice/orderrequest " + _ex.Message);
                                    _logger.LogError(_ex, _ex.Message);
                                }
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex.Message);
                    _logger.LogError(ex, ex.Message);
                }
            }
            await SendStoreInfo();
            //for test
            //string strTestData = "\"{\\\"id\\\":5677683867717,\\\"admin_graphql_api_id\\\":\\\"gid:\\\\/\\\\/shopify\\\\/Order\\\\/5677683867717\\\",\\\"app_id\\\":580111,\\\"browser_ip\\\":\\\"174.0.75.3\\\",\\\"buyer_accepts_marketing\\\":true,\\\"cancel_reason\\\":null,\\\"cancelled_at\\\":null,\\\"cart_token\\\":\\\"Z2NwLXVzLXdlc3QxOjAxSkRBTTVIN0ZKNVpCSjNQUVEzMEFTNVFT\\\",\\\"checkout_id\\\":28598579527749,\\\"checkout_token\\\":\\\"256ea6aa1963b23b2721fdcc5f4a032d\\\",\\\"client_details\\\":{\\\"accept_language\\\":\\\"en-CA\\\",\\\"browser_height\\\":null,\\\"browser_ip\\\":\\\"174.0.75.3\\\",\\\"browser_width\\\":null,\\\"session_hash\\\":null,\\\"user_agent\\\":\\\"Mozilla\\\\/5.0 (iPad; CPU OS 16_7 like Mac OS X) AppleWebKit\\\\/605.1.15 (KHTML, like Gecko) GSA\\\\/343.0.695551749 Mobile\\\\/15E148 Safari\\\\/604.1\\\"},\\\"closed_at\\\":null,\\\"company\\\":null,\\\"confirmation_number\\\":\\\"ML79MWIM0\\\",\\\"confirmed\\\":true,\\\"contact_email\\\":\\\"darlene.ohlhauser@gmail.com\\\",\\\"created_at\\\":\\\"2024-11-22T14:18:25-05:00\\\",\\\"currency\\\":\\\"CAD\\\",\\\"current_shipping_price_set\\\":{\\\"shop_money\\\":{\\\"amount\\\":\\\"1.99\\\",\\\"currency_code\\\":\\\"CAD\\\"},\\\"presentment_money\\\":{\\\"amount\\\":\\\"1.99\\\",\\\"currency_code\\\":\\\"CAD\\\"}},\\\"current_subtotal_price\\\":\\\"99.98\\\",\\\"current_subtotal_price_set\\\":{\\\"shop_money\\\":{\\\"amount\\\":\\\"99.98\\\",\\\"currency_code\\\":\\\"CAD\\\"},\\\"presentment_money\\\":{\\\"amount\\\":\\\"99.98\\\",\\\"currency_code\\\":\\\"CAD\\\"}},\\\"current_total_additional_fees_set\\\":null,\\\"current_total_discounts\\\":\\\"25.00\\\",\\\"current_total_discounts_set\\\":{\\\"shop_money\\\":{\\\"amount\\\":\\\"25.00\\\",\\\"currency_code\\\":\\\"CAD\\\"},\\\"presentment_money\\\":{\\\"amount\\\":\\\"25.00\\\",\\\"currency_code\\\":\\\"CAD\\\"}},\\\"current_total_duties_set\\\":null,\\\"current_total_price\\\":\\\"101.97\\\",\\\"current_total_price_set\\\":{\\\"shop_money\\\":{\\\"amount\\\":\\\"101.97\\\",\\\"currency_code\\\":\\\"CAD\\\"},\\\"presentment_money\\\":{\\\"amount\\\":\\\"101.97\\\",\\\"currency_code\\\":\\\"CAD\\\"}},\\\"current_total_tax\\\":\\\"0.00\\\",\\\"current_total_tax_set\\\":{\\\"shop_money\\\":{\\\"amount\\\":\\\"0.00\\\",\\\"currency_code\\\":\\\"CAD\\\"},\\\"presentment_money\\\":{\\\"amount\\\":\\\"0.00\\\",\\\"currency_code\\\":\\\"CAD\\\"}},\\\"customer_locale\\\":\\\"en-CA\\\",\\\"device_id\\\":null,\\\"discount_codes\\\":[{\\\"code\\\":\\\"BFCM\\\",\\\"amount\\\":\\\"25.00\\\",\\\"type\\\":\\\"percentage\\\"}],\\\"duties_included\\\":false,\\\"email\\\":\\\"darlene.ohlhauser@gmail.com\\\",\\\"estimated_taxes\\\":false,\\\"financial_status\\\":\\\"paid\\\",\\\"fulfillment_status\\\":null,\\\"landing_site\\\":\\\"\\\\/products\\\\/punkjuice-iphone-8-7-plus-6s-plus-6-plus-battery-case-teal-waterproof-slim-power-juice-bank-with-4300mah-teal?variant=12156584067180\\\\u0026currency=CAD\\\\u0026utm_source=google\\\\u0026utm_medium=cpc\\\\u0026utm_campaign=google+shopping\\\\u0026utm_source=google\\\\u0026utm_medium=cpc\\\\u0026utm_campaign=21744630648\\\\u0026utm_content=\\\\u0026utm_term=\\\\u0026gad_source=1\\\\u0026gclid=EAIaIQobChMImY2snNDwiQMVQiitBh2msQ_0EAQYAiABEgK59_D_BwE\\\",\\\"landing_site_ref\\\":null,\\\"location_id\\\":null,\\\"merchant_business_entity_id\\\":\\\"MTIxNTc3NDA5\\\",\\\"merchant_of_record_app_id\\\":null,\\\"name\\\":\\\"#CA39752\\\",\\\"note\\\":null,\\\"note_attributes\\\":[],\\\"number\\\":38752,\\\"order_number\\\":39752,\\\"order_status_url\\\":\\\"https:\\\\/\\\\/punkcase.ca\\\\/21577409\\\\/orders\\\\/1f4462242e77d837e6b37c6441442d50\\\\/authenticate?key=242b2fd2709420c4e1f7dac819e52b6a\\\",\\\"original_total_additional_fees_set\\\":null,\\\"original_total_duties_set\\\":null,\\\"payment_gateway_names\\\":[\\\"shopify_payments\\\"],\\\"phone\\\":null,\\\"po_number\\\":null,\\\"presentment_currency\\\":\\\"CAD\\\",\\\"processed_at\\\":\\\"2024-11-22T14:18:21-05:00\\\",\\\"reference\\\":\\\"6f46bd312c566d35f90aa38e9149f8c9\\\",\\\"referring_site\\\":\\\"https:\\\\/\\\\/www.google.com\\\\/\\\",\\\"source_identifier\\\":\\\"6f46bd312c566d35f90aa38e9149f8c9\\\",\\\"source_name\\\":\\\"web\\\",\\\"source_url\\\":null,\\\"subtotal_price\\\":\\\"99.98\\\",\\\"subtotal_price_set\\\":{\\\"shop_money\\\":{\\\"amount\\\":\\\"99.98\\\",\\\"currency_code\\\":\\\"CAD\\\"},\\\"presentment_money\\\":{\\\"amount\\\":\\\"99.98\\\",\\\"currency_code\\\":\\\"CAD\\\"}},\\\"tags\\\":\\\"\\\",\\\"tax_exempt\\\":false,\\\"tax_lines\\\":[],\\\"taxes_included\\\":false,\\\"test\\\":false,\\\"token\\\":\\\"1f4462242e77d837e6b37c6441442d50\\\",\\\"total_cash_rounding_payment_adjustment_set\\\":{\\\"shop_money\\\":{\\\"amount\\\":\\\"0.00\\\",\\\"currency_code\\\":\\\"CAD\\\"},\\\"presentment_money\\\":{\\\"amount\\\":\\\"0.00\\\",\\\"currency_code\\\":\\\"CAD\\\"}},\\\"total_cash_rounding_refund_adjustment_set\\\":{\\\"shop_money\\\":{\\\"amount\\\":\\\"0.00\\\",\\\"currency_code\\\":\\\"CAD\\\"},\\\"presentment_money\\\":{\\\"amount\\\":\\\"0.00\\\",\\\"currency_code\\\":\\\"CAD\\\"}},\\\"total_discounts\\\":\\\"25.00\\\",\\\"total_discounts_set\\\":{\\\"shop_money\\\":{\\\"amount\\\":\\\"25.00\\\",\\\"currency_code\\\":\\\"CAD\\\"},\\\"presentment_money\\\":{\\\"amount\\\":\\\"25.00\\\",\\\"currency_code\\\":\\\"CAD\\\"}},\\\"total_line_items_price\\\":\\\"124.98\\\",\\\"total_line_items_price_set\\\":{\\\"shop_money\\\":{\\\"amount\\\":\\\"124.98\\\",\\\"currency_code\\\":\\\"CAD\\\"},\\\"presentment_money\\\":{\\\"amount\\\":\\\"124.98\\\",\\\"currency_code\\\":\\\"CAD\\\"}},\\\"total_outstanding\\\":\\\"0.00\\\",\\\"total_price\\\":\\\"101.97\\\",\\\"total_price_set\\\":{\\\"shop_money\\\":{\\\"amount\\\":\\\"101.97\\\",\\\"currency_code\\\":\\\"CAD\\\"},\\\"presentment_money\\\":{\\\"amount\\\":\\\"101.97\\\",\\\"currency_code\\\":\\\"CAD\\\"}},\\\"total_shipping_price_set\\\":{\\\"shop_money\\\":{\\\"amount\\\":\\\"1.99\\\",\\\"currency_code\\\":\\\"CAD\\\"},\\\"presentment_money\\\":{\\\"amount\\\":\\\"1.99\\\",\\\"currency_code\\\":\\\"CAD\\\"}},\\\"total_tax\\\":\\\"0.00\\\",\\\"total_tax_set\\\":{\\\"shop_money\\\":{\\\"amount\\\":\\\"0.00\\\",\\\"currency_code\\\":\\\"CAD\\\"},\\\"presentment_money\\\":{\\\"amount\\\":\\\"0.00\\\",\\\"currency_code\\\":\\\"CAD\\\"}},\\\"total_tip_received\\\":\\\"0.00\\\",\\\"total_weight\\\":256,\\\"updated_at\\\":\\\"2024-11-22T14:18:29-05:00\\\",\\\"user_id\\\":null,\\\"billing_address\\\":{\\\"first_name\\\":\\\"Darlene\\\",\\\"address1\\\":\\\"640 Upper Lakeview Road\\\",\\\"phone\\\":\\\"14039570932\\\",\\\"city\\\":\\\"Invermere\\\",\\\"zip\\\":\\\"V0A 1K3\\\",\\\"province\\\":\\\"British Columbia\\\",\\\"country\\\":\\\"Canada\\\",\\\"last_name\\\":\\\"Ohlhauser\\\",\\\"address2\\\":\\\"#45\\\",\\\"company\\\":null,\\\"latitude\\\":null,\\\"longitude\\\":null,\\\"name\\\":\\\"Darlene Ohlhauser\\\",\\\"country_code\\\":\\\"CA\\\",\\\"province_code\\\":\\\"BC\\\"},\\\"customer\\\":{\\\"id\\\":7042609709125,\\\"email\\\":\\\"darlene.ohlhauser@gmail.com\\\",\\\"created_at\\\":\\\"2024-11-22T14:18:22-05:00\\\",\\\"updated_at\\\":\\\"2024-11-22T14:18:26-05:00\\\",\\\"first_name\\\":\\\"Darlene\\\",\\\"last_name\\\":\\\"Ohlhauser\\\",\\\"state\\\":\\\"disabled\\\",\\\"note\\\":null,\\\"verified_email\\\":true,\\\"multipass_identifier\\\":null,\\\"tax_exempt\\\":false,\\\"phone\\\":null,\\\"email_marketing_consent\\\":{\\\"state\\\":\\\"subscribed\\\",\\\"opt_in_level\\\":\\\"single_opt_in\\\",\\\"consent_updated_at\\\":\\\"2024-11-22T14:18:26-05:00\\\"},\\\"sms_marketing_consent\\\":null,\\\"tags\\\":\\\"\\\",\\\"currency\\\":\\\"CAD\\\",\\\"tax_exemptions\\\":[],\\\"admin_graphql_api_id\\\":\\\"gid:\\\\/\\\\/shopify\\\\/Customer\\\\/7042609709125\\\",\\\"default_address\\\":{\\\"id\\\":8278519611461,\\\"customer_id\\\":7042609709125,\\\"first_name\\\":\\\"Darlene\\\",\\\"last_name\\\":\\\"Ohlhauser\\\",\\\"company\\\":null,\\\"address1\\\":\\\"640 Upper Lakeview Road\\\",\\\"address2\\\":\\\"#45\\\",\\\"city\\\":\\\"Invermere\\\",\\\"province\\\":\\\"British Columbia\\\",\\\"country\\\":\\\"Canada\\\",\\\"zip\\\":\\\"V0A 1K3\\\",\\\"phone\\\":\\\"14039570932\\\",\\\"name\\\":\\\"Darlene Ohlhauser\\\",\\\"province_code\\\":\\\"BC\\\",\\\"country_code\\\":\\\"CA\\\",\\\"country_name\\\":\\\"Canada\\\",\\\"default\\\":true}},\\\"discount_applications\\\":[{\\\"target_type\\\":\\\"line_item\\\",\\\"type\\\":\\\"discount_code\\\",\\\"value\\\":\\\"20.0\\\",\\\"value_type\\\":\\\"percentage\\\",\\\"allocation_method\\\":\\\"across\\\",\\\"target_selection\\\":\\\"all\\\",\\\"code\\\":\\\"BFCM\\\"}],\\\"fulfillments\\\":[],\\\"line_items\\\":[{\\\"id\\\":13785236144197,\\\"admin_graphql_api_id\\\":\\\"gid:\\\\/\\\\/shopify\\\\/LineItem\\\\/13785236144197\\\",\\\"current_quantity\\\":1,\\\"fulfillable_quantity\\\":0,\\\"fulfillment_service\\\":\\\"manual\\\",\\\"fulfillment_status\\\":null,\\\"gift_card\\\":false,\\\"grams\\\":255,\\\"name\\\":\\\"PunkJuice iPhone 8+\\\\/7+Plus Battery Case Black - Waterproof Slim Power Juice Bank with 4300mAh - Black\\\",\\\"pre_tax_price\\\":\\\"99.98\\\",\\\"pre_tax_price_set\\\":{\\\"shop_money\\\":{\\\"amount\\\":\\\"99.98\\\",\\\"currency_code\\\":\\\"CAD\\\"},\\\"presentment_money\\\":{\\\"amount\\\":\\\"99.98\\\",\\\"currency_code\\\":\\\"CAD\\\"}},\\\"price\\\":\\\"124.98\\\",\\\"price_set\\\":{\\\"shop_money\\\":{\\\"amount\\\":\\\"124.98\\\",\\\"currency_code\\\":\\\"CAD\\\"},\\\"presentment_money\\\":{\\\"amount\\\":\\\"124.98\\\",\\\"currency_code\\\":\\\"CAD\\\"}},\\\"product_exists\\\":true,\\\"product_id\\\":34702688264,\\\"properties\\\":[],\\\"quantity\\\":1,\\\"requires_shipping\\\":true,\\\"sku\\\":\\\"PUNK-i8P-PJWTP-A520\\\",\\\"taxable\\\":true,\\\"title\\\":\\\"PunkJuice iPhone 8+\\\\/7+Plus Battery Case Black - Waterproof Slim Power Juice Bank with 4300mAh\\\",\\\"total_discount\\\":\\\"0.00\\\",\\\"total_discount_set\\\":{\\\"shop_money\\\":{\\\"amount\\\":\\\"0.00\\\",\\\"currency_code\\\":\\\"CAD\\\"},\\\"presentment_money\\\":{\\\"amount\\\":\\\"0.00\\\",\\\"currency_code\\\":\\\"CAD\\\"}},\\\"variant_id\\\":543984680968,\\\"variant_inventory_management\\\":\\\"shopify\\\",\\\"variant_title\\\":\\\"Black\\\",\\\"vendor\\\":\\\"PunkCase\\\",\\\"tax_lines\\\":[],\\\"duties\\\":[],\\\"discount_allocations\\\":[{\\\"amount\\\":\\\"25.00\\\",\\\"amount_set\\\":{\\\"shop_money\\\":{\\\"amount\\\":\\\"25.00\\\",\\\"currency_code\\\":\\\"CAD\\\"},\\\"presentment_money\\\":{\\\"amount\\\":\\\"25.00\\\",\\\"currency_code\\\":\\\"CAD\\\"}},\\\"discount_application_index\\\":0}]}],\\\"payment_terms\\\":null,\\\"refunds\\\":[],\\\"shipping_address\\\":{\\\"first_name\\\":\\\"Darlene\\\",\\\"address1\\\":\\\"640 Upper Lakeview Road\\\",\\\"phone\\\":\\\"14039570932\\\",\\\"city\\\":\\\"Invermere\\\",\\\"zip\\\":\\\"V0A 1K3\\\",\\\"province\\\":\\\"British Columbia\\\",\\\"country\\\":\\\"Canada\\\",\\\"last_name\\\":\\\"Ohlhauser\\\",\\\"address2\\\":\\\"#45\\\",\\\"company\\\":null,\\\"latitude\\\":50.5065399,\\\"longitude\\\":-116.009696,\\\"name\\\":\\\"Darlene Ohlhauser\\\",\\\"country_code\\\":\\\"CA\\\",\\\"province_code\\\":\\\"BC\\\"},\\\"shipping_lines\\\":[{\\\"id\\\":4816548855877,\\\"carrier_identifier\\\":null,\\\"code\\\":\\\"First Class Shipping\\\",\\\"current_discounted_price_set\\\":{\\\"shop_money\\\":{\\\"amount\\\":\\\"1.99\\\",\\\"currency_code\\\":\\\"CAD\\\"},\\\"presentment_money\\\":{\\\"amount\\\":\\\"1.99\\\",\\\"currency_code\\\":\\\"CAD\\\"}},\\\"discounted_price\\\":\\\"1.99\\\",\\\"discounted_price_set\\\":{\\\"shop_money\\\":{\\\"amount\\\":\\\"1.99\\\",\\\"currency_code\\\":\\\"CAD\\\"},\\\"presentment_money\\\":{\\\"amount\\\":\\\"1.99\\\",\\\"currency_code\\\":\\\"CAD\\\"}},\\\"is_removed\\\":false,\\\"phone\\\":null,\\\"price\\\":\\\"1.99\\\",\\\"price_set\\\":{\\\"shop_money\\\":{\\\"amount\\\":\\\"1.99\\\",\\\"currency_code\\\":\\\"CAD\\\"},\\\"presentment_money\\\":{\\\"amount\\\":\\\"1.99\\\",\\\"currency_code\\\":\\\"CAD\\\"}},\\\"requested_fulfillment_service_id\\\":null,\\\"source\\\":\\\"shopify\\\",\\\"title\\\":\\\"First Class Shipping\\\",\\\"tax_lines\\\":[],\\\"discount_allocations\\\":[]}],\\\"returns\\\":[]}\"";
            //await SaveNewOrder(strTestData);
        }

        public async Task<List<TbShopifyToken>> GetShopifyTokens()
        {
            using (var scope = _serviceScopeFactory.CreateScope())  // Create a new scope
            {
                var _dbContext = scope.ServiceProvider.GetRequiredService<AppIdentityDbContext>();
                return await _dbContext.TbTokens.ToListAsync();
            }
        }

        public async Task<List<TbOrder>> GetOrders(string strOwner, int nPageNo, int pageSize)
        {
            using (var scope = _serviceScopeFactory.CreateScope())  // Create a new scope
            {
                var _dbContext = scope.ServiceProvider.GetRequiredService<AppIdentityDbContext>();                
                return await _dbContext.TbOrders.Where(e => e.or_owner == strOwner).OrderByDescending(e => e.or_date).Skip((nPageNo - 1) * pageSize).Take(pageSize).ToListAsync();
            }
        }

        public async Task<int> GetOrdersPerPageCnt(string strDomain, int nPageNo, int pageSize)
        {
            int nCnt = 0;
            try
            {
                using (var scope = _serviceScopeFactory.CreateScope())
                {
                    var dbContext = scope.ServiceProvider.GetRequiredService<AppIdentityDbContext>();

                    nCnt = await dbContext.TbOrders.Where(e => e.or_owner == strDomain).CountAsync();

                    return nCnt;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.StackTrace);
                _logger.LogError(ex, ex.Message);
            }
            return nCnt;
        }
        
        public async Task RegisterHookEntry(string shopUrl, string accessToken)
        {
            _logger.LogInformation("RegisterHookEntry called.");
            Dictionary<string, string> mapHooks = new Dictionary<string, string>()
            {
                { "orders/create", $"{_domain}shopify/order_create" },
                { "orders/updated", "" },
                { "orders/paid", "" },
                { "orders/fulfilled", $"{_domain}shopify/order_create" },
                { "orders/cancelled", $"{_domain}shopify/order_cancelled" },
                { "orders/partially_fulfilled", "" },
                { "order_transactions/create", "" },
            };
            foreach(var key in mapHooks)
            {
                if (string.IsNullOrEmpty(key.Value)) continue;
                await RegisterWebhookAsync(shopUrl, accessToken, key.Key, key.Value);
                _logger.LogInformation("RegisterWebhookAsync called.");
            }
            
        }

        private async Task RegisterWebhookAsync(string shopUrl, string accessToken, string topic, string callbackUrl)
        {
            string urlOrders = $"https://{shopUrl}/admin/api/{_apiVersion}/webhooks.json";
            _logger.LogInformation(urlOrders);
            var webhookPayload = new
            {
                webhook = new
                {
                    topic = topic,
                    address = callbackUrl,
                    format = "json"
                }
            };

            var jsonPayload = JsonSerializer.Serialize(webhookPayload);
            var content = new StringContent(jsonPayload, Encoding.UTF8, "application/json");
            _logger.LogInformation(topic);
            _logger.LogInformation(callbackUrl);
            using (HttpClient _httpClient = new HttpClient())
            {
                _httpClient.DefaultRequestHeaders.Clear();
                _httpClient.DefaultRequestHeaders.Add("X-Shopify-Access-Token", accessToken);
                _logger.LogInformation($"Webhook create accest toke ins {urlOrders}");
                try
                {
                    var response = await _httpClient.PostAsync(urlOrders, content);
                    if (response.IsSuccessStatusCode)
                    {
                        Console.WriteLine("Webhook registered successfully.");
                        _logger.LogInformation("Webhook registered successfully.");
                    }
                    else
                    {
                        var errorResponse = await response.Content.ReadAsStringAsync();
                        _logger.LogInformation($"Failed to register webhook: {errorResponse}");
                    }
                }catch(Exception ex)
                {
                    Console.WriteLine("shopifyservice/registrewebhook " + ex.Message);
                    _logger.LogError(ex, ex.Message);
                }
            }
        }

        private KeyValuePair<string, string> GetAccessTokenByOrder(string orderNum)
        {
            using (var scope = _serviceScopeFactory.CreateScope())  // Create a new scope
            {
                var _dbContext = scope.ServiceProvider.GetRequiredService<AppIdentityDbContext>();
                try
                {
                    var result = from order in _dbContext.TbOrders
                                 join token in _dbContext.TbTokens
                                 on order.or_owner equals token.ShopDomain
                                 where order.or_name == orderNum
                                 select new
                                 {
                                     accessToken = token.AccessToken,
                                     shopDomain = token.ShopDomain
                                 };
                    if(result.Count() > 0 )
                    {
                        return new KeyValuePair<string, string>(result.First().accessToken , result.First().shopDomain);
                    }
                    return new KeyValuePair<string, string>();
                }catch(Exception e)
                {
                    Console.WriteLine("shopifyservice/getaccesstokenbyorder " + e.Message);
                    _logger.LogError (e, e.Message);
                }
                return new KeyValuePair<string, string>();
            }
        }

		private KeyValuePair<string, string> GetAccessTokenByOrder(long nOrderId)
		{
			using (var scope = _serviceScopeFactory.CreateScope())  // Create a new scope
			{
				var _dbContext = scope.ServiceProvider.GetRequiredService<AppIdentityDbContext>();
				try
				{
					var result = from order in _dbContext.TbOrders
								 join token in _dbContext.TbTokens
								 on order.or_owner equals token.ShopDomain
								 where order.or_id == nOrderId
								 select new
								 {
									 accessToken = token.AccessToken,
									 shopDomain = token.ShopDomain
								 };
					if (result.Count() > 0)
					{
						return new KeyValuePair<string, string>(result.First().accessToken, result.First().shopDomain);
					}
					return new KeyValuePair<string, string>();
				}
				catch (Exception e)
				{
					Console.WriteLine(e.Message);
					_logger.LogError(e, e.Message);
				}
				return new KeyValuePair<string, string>();
			}
		}

		public async Task SaveNewOrder(string strOrderJson)
        {
            var order = JsonDocument.Parse(strOrderJson).RootElement;
            using (var scope = _serviceScopeFactory.CreateScope())  // Create a new scope
            {
                var _dbContext = scope.ServiceProvider.GetRequiredService<AppIdentityDbContext>();

                {
                    try
                    {
                        var customerElement = order.GetProperty("customer");
                        var line_items = order.GetProperty("line_items");
                        int nCnt = 0;
                        if (line_items.ValueKind == JsonValueKind.Array)
                        {
                            foreach (var line_item in line_items.EnumerateArray())
                            {
                                nCnt += Convert.ToInt32(line_item.GetProperty("quantity").ToString());
                            }
                        }
                        string strPaymentStatus = order.GetProperty("financial_status").ToString();
                        string strFulfillStatus = order.TryGetProperty("fulfillment_status", out var fulfillmentStatusElement)
                            ? (fulfillmentStatusElement.ValueKind == JsonValueKind.Null ? "null" : fulfillmentStatusElement.GetString())
                            : "null";

                        long or_id = Convert.ToInt64(order.GetProperty("id").ToString());
                        string or_name = order.GetProperty("name").ToString() ?? "";
                        string or_channel = "online store";//order.GetProperty("channel").ToString(),
                        string or_customer = customerElement.GetProperty("email").ToString();
                        string or_customer_name = $"{customerElement.GetProperty("first_name").ToString()} {customerElement.GetProperty("last_name").ToString()}";
                        string or_phone = customerElement.GetProperty("phone").ToString();
                        if (string.IsNullOrEmpty(or_phone))
                        {
                            var defaultAddress = customerElement.GetProperty("default_address");
                            if (defaultAddress.ValueKind != JsonValueKind.Null)
                            {
                                or_phone = defaultAddress.GetProperty("phone").ToString();
                            }
                        }
                        float or_total = Convert.ToSingle(order.GetProperty("total_price").ToString());
                        int or_itemCnt = nCnt;
                        string or_owner = "";
                        if (order.TryGetProperty("order_status_url", out JsonElement orderStatusUrl))
                        {
                            string url = orderStatusUrl.GetString();

                            if (Uri.TryCreate(url, UriKind.Absolute, out Uri uri))
                            {
                                url = uri.Host;
                                or_owner = url.Replace(".", "") + ".myshopify.com";
                            }
                        }
                        int or_payment_status = _mapPaymentStatus[strPaymentStatus];
                        int or_fulfill_status = _mapFulfiilmentStatus[strFulfillStatus];
                        var dateString = order.GetProperty("created_at").ToString();
                        dateString = dateString.Replace(" ", "");
                        DateTime or_date = DateTime.Parse(dateString);
                        int or_status = 0;
                        if (order.TryGetProperty("cancel_reason", out JsonElement cancelReason))
                        {
                            if (cancelReason.ValueKind != JsonValueKind.Null)
                            {
                                if (order.TryGetProperty("cancelled_at", out JsonElement cancelDate))
                                {
                                    if (cancelDate.ValueKind != JsonValueKind.Null)
                                    {
                                        or_status = 2;
                                    }
                                }
                            }
                        }
                        TbOrder pOrder = new TbOrder
                        {
                            or_id = or_id,
                            or_name = or_name,
                            or_channel = or_channel,
                            or_customer = or_customer,
                            or_total = or_total,
                            or_itemCnt = or_itemCnt,
                            or_owner = or_owner,
                            or_payment_status = or_payment_status,
                            or_fulfill_status = or_fulfill_status,
                            or_date = or_date,
                            or_status = or_status,
                            or_customer_name = or_customer_name,
                            or_phone = or_phone,
                        };
                        _logger.LogInformation($"or_id is {or_id}");
                        TbOrder _pOrigin = await _dbContext.TbOrders.Where(e => e.or_id == pOrder.or_id).FirstOrDefaultAsync();
                        if (_pOrigin == null)
                        {
                            _dbContext.TbOrders.Add(pOrder);
                            _logger.LogInformation("new order get added!");
                        }
                        else
                        {
                            _pOrigin.or_customer = or_customer;
                            _pOrigin.or_date = or_date;
                            _pOrigin.or_status = or_status;
                            _pOrigin.or_fulfill_status = or_fulfill_status;
                            _pOrigin.or_payment_status = or_payment_status;
                            _pOrigin.or_phone = or_phone;
                            _pOrigin.or_customer_name = or_customer_name;

                            _logger.LogInformation("order get changed!");
                        }
                        int nRes = await _dbContext.SaveChangesAsync();
                        _logger.LogInformation($"SaveChange result is {nRes}");
                        await SendStoreInfo();						
					}
                    catch (Exception _ex)
                    {
                        Console.WriteLine(_ex.Message);
                        _logger.LogError(_ex, _ex.Message);
                    }
                }
            }
        }
       
        public async Task<KeyValuePair<string, string>> GetOwnerInfo(string shop, string accessToken)
        {
            string urlOrders = $"https://{shop}/admin/api/{_apiVersion}/shop.json";

            KeyValuePair<string, string> mapResult = new KeyValuePair<string, string>();
            using (HttpClient client = new HttpClient())
            {
                client.DefaultRequestHeaders.Add("X-Shopify-Access-Token", accessToken);

                try
                {
                    HttpResponseMessage response = await client.GetAsync(urlOrders);

                    if (response.IsSuccessStatusCode)
                    {
                        string responseData = await response.Content.ReadAsStringAsync();
                        Console.WriteLine(responseData);
                        _logger.LogInformation(responseData);
                    }
                }
                catch (Exception ex)
                {
                    Console.Write(ex.ToString());
                    _logger.LogError(ex, ex.Message);
                }
            }
            return mapResult;
        }

        public async Task<List<OrderRequest>> UpdateOrder()
        {
            using (var scope = _serviceScopeFactory.CreateScope())  // Create a new scope
            {
                var _dbContext = scope.ServiceProvider.GetRequiredService<AppIdentityDbContext>();

                List<TbShopifyToken> lstToken = await _dbContext.TbTokens.ToListAsync();
                List<OrderRequest> lstResult = new List<OrderRequest>();
                try
                {
                    foreach(TbShopifyToken token in lstToken)
                    {
                        OrderRequest p = new OrderRequest
                        {
                            count = await _dbContext.TbOrders.Where(e => e.or_fulfill_status == 0 && e.or_status != 2 && e.or_owner == token.ShopDomain).CountAsync(),
                            token = token.AccessToken,
                            store = token.ShopDomain
                        };
                        lstResult.Add(p);
                    }
                }
                catch(Exception ex)
                {
                    Console.WriteLine("shopifyservice/updateorder " + ex.Message);
                    _logger.LogError(ex, ex.Message);
                }
                return lstResult;
            }
        }

        private async Task GetCustomers(string shop, string accessToken)
        {
            string urlOrders = $"https://{shop}/admin/api/{_apiVersion}/customers.json";

            using (HttpClient client = new HttpClient())
            {
                client.DefaultRequestHeaders.Add("X-Shopify-Access-Token", accessToken);

                try
                {
                    HttpResponseMessage response = await client.GetAsync(urlOrders);

                    if (response.IsSuccessStatusCode)
                    {
                        string responseData = await response.Content.ReadAsStringAsync();
                        var orders = JsonDocument.Parse(responseData).RootElement.GetProperty("customers");
                        using (var scope = _serviceScopeFactory.CreateScope())  // Create a new scope
                        {
                            var _dbContext = scope.ServiceProvider.GetRequiredService<AppIdentityDbContext>();
                            foreach (var order in orders.EnumerateArray())
                            {
                                try
                                {
                                    var emailElement = order.GetProperty("email");

                                    if (emailElement.ValueKind == JsonValueKind.Null) continue;

                                    string strEmail = emailElement.ToString();
                                    var firstName = order.GetProperty("first_name").ToString();
                                    var lastName = order.GetProperty("last_name").ToString();
                                    string phone = order.GetProperty("phone").ToString();
                                    var defaultAddress = order.GetProperty("default_address");
                                    string address1 = "", address2 = "", city = "", province = "", country = "", province_code = "", zip = "";

                                    if (!string.IsNullOrEmpty(phone))
                                    {
                                        if(defaultAddress.ValueKind != JsonValueKind.Null)
                                        {
                                            phone = defaultAddress.GetProperty("phone").ToString();
                                            address1 = defaultAddress.GetProperty("address1").ToString();
                                            address2 = defaultAddress.GetProperty("address2").ToString();
                                            city = defaultAddress.GetProperty("city").ToString();
                                            province = defaultAddress.GetProperty("province").ToString();
                                            country = defaultAddress.GetProperty("country").ToString();
                                            province_code = defaultAddress.GetProperty("province_code").ToString();
                                            zip = defaultAddress.GetProperty("zip").ToString();
                                        }
                                    }
                                    var dateString = order.GetProperty("created_at").ToString();
                                    dateString = dateString.Replace(" ", "");
                                    DateTime createdAt = DateTime.Parse(dateString);
                                    dateString = order.GetProperty("updated_at").ToString();
                                    dateString = dateString.Replace(" ", "");
                                    DateTime updatedAt = DateTime.Parse(dateString);
                                    string user_id = order.GetProperty("id").ToString();

                                    TbShopifyUser p = new TbShopifyUser
                                    {
                                        UserId = strEmail,
                                        UserName = $"{firstName} {lastName}",
                                        UserShopifyDomain = shop,
                                        phone = phone,
                                        User_Id = user_id,
                                        createdAt = createdAt,
                                        updatedAt = updatedAt,
                                        address1 = address1,
                                        address2 = address2,
                                        city = city,
                                        province = province,
                                        country = country,
                                        province_code = province_code,
                                        zip = zip,
                                    };

                                    TbShopifyUser _p = await _dbContext.TbShopifyUsers.Where(e => e.UserId== p.UserId).FirstOrDefaultAsync();
                                    if (_p == null)
                                    {
                                        _dbContext.TbShopifyUsers.Add(p);
                                    }
                                    else
                                    {
                                        _p.phone = phone;
                                        _p.User_Id = user_id;
                                        _p.createdAt = createdAt;
                                        _p.updatedAt = updatedAt;
                                        _p.address1 = address1;
                                        _p.address2 = address2;
                                        _p.city = city;
                                        _p.province = province;
                                        _p.country = country;
                                        _p.province_code = province_code;
                                        _p.zip = zip;
                                    }    
                                    await _dbContext.SaveChangesAsync();
                                }
                                catch (Exception ex)
                                {
                                    Console.WriteLine(ex.Message);
                                }
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.Write(ex.ToString());
                    _logger.LogError(ex, ex.Message);
                }
            }
        }

        public async Task CustomersRequest()
        {
            using (var scope = _serviceScopeFactory.CreateScope())  // Create a new scope
            {
                var _dbContext = scope.ServiceProvider.GetRequiredService<AppIdentityDbContext>();
                foreach (var objToken in _dbContext.TbTokens)
                {
                    if (objToken == null) continue;
                    if (string.IsNullOrEmpty(objToken.ShopDomain) || string.IsNullOrEmpty(objToken.AccessToken)) continue;
                    await GetCustomers(objToken.ShopDomain, objToken.AccessToken);
                }
            }
        }

        public async Task<string> GetAccessTokenByStore(string strStore)
        {
            using (var scope = _serviceScopeFactory.CreateScope())  // Create a new scope
            {
                var _dbContext = scope.ServiceProvider.GetRequiredService<AppIdentityDbContext>();
                try
                {
                    var result = await _dbContext.TbTokens.Where(e => e.ShopDomain == strStore).FirstOrDefaultAsync();
                    if (result != null)
                    {
                        return result.AccessToken;
                    }
                    return "";
                }
                catch (Exception e)
                {
                    Console.WriteLine(e.Message);
                    _logger.LogError(e, e.Message);
                }
                return "";
            }
        }
    
        public TbOrder GetOrderInfo(string strOrderId)
        {
            using (var scope = _serviceScopeFactory.CreateScope())  // Create a new scope
            {
                string strOrderNum = AddPrefixIfMissing(strOrderId);
                Console.WriteLine(strOrderNum);
                var _dbContext = scope.ServiceProvider.GetRequiredService<AppIdentityDbContext>();
                return _dbContext.TbOrders.Where(e => e.or_name == strOrderNum).FirstOrDefault();
            }
        }

		public TbOrder GetOrderInfo(long nOrderId)
		{
			using (var scope = _serviceScopeFactory.CreateScope())  // Create a new scope
			{				
				var _dbContext = scope.ServiceProvider.GetRequiredService<AppIdentityDbContext>();
                return _dbContext.TbOrders.Where(e => e.or_id == nOrderId).FirstOrDefault();
			}
		}

		public TbOrder GetOrderInfoByEmail(string strEmail)
        {
			using (var scope = _serviceScopeFactory.CreateScope())  // Create a new scope
			{	
				var _dbContext = scope.ServiceProvider.GetRequiredService<AppIdentityDbContext>();
				return _dbContext.TbOrders.Where(e => !string.IsNullOrEmpty(e.or_customer) && strEmail.Contains(e.or_customer)).OrderByDescending(e => e.or_date).FirstOrDefault();
			}
		}

        public TbOrder GetOrderInfoByPhone(string phone)
        {
            using (var scope = _serviceScopeFactory.CreateScope())  // Create a new scope
            {
                var _dbContext = scope.ServiceProvider.GetRequiredService<AppIdentityDbContext>();
                phone = NormalizePhoneNumber_(phone);

				foreach (var e in _dbContext.TbOrders.Where(e => !string.IsNullOrEmpty(e.or_phone)).OrderByDescending(e => e.or_date).ToList())
                {
                    string _p = NormalizePhoneNumber_(e.or_phone);
                    if(phone == _p)
                    {
                        return e;
                    }
                }
                //return await _dbContext.TbOrders.Where(e => !string.IsNullOrEmpty(e.or_phone) && new PhoneNumber(phone).ToString() == new PhoneNumber(e.or_phone).ToString()).OrderByDescending(e => e.or_date).FirstOrDefaultAsync();
                return null;
            }
        }

        public async Task SendStoreInfo()
        {
            try
			{
				List<OrderRequest> ptRequest = await UpdateOrder();
				var objPacket = new
				{
					orders = ptRequest,
					type = "store"
				};
				string strJson = System.Text.Json.JsonSerializer.Serialize(objPacket);
				await _hubContext.Clients.All.SendAsync("ReceiveMessage", "", strJson);
			}
			catch(Exception e)
            {
                Console.WriteLine("shopifyservice/sendstoreinfo " + e.Message);
            }
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
}
