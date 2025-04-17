using System.Collections.Concurrent;
using System.Net.WebSockets;
using System.Text;

namespace GoogleLogin.Services
{
    public class WebSocketHandler
    {
        private static ConcurrentDictionary<string, WebSocket> _connectedClients = new ConcurrentDictionary<string, WebSocket>();

        public static async Task HandleWebSocketAsync(WebSocket webSocket)
        {
            var clientId = Guid.NewGuid().ToString();
            _connectedClients.TryAdd(clientId, webSocket);

            try
            {
                var buffer = new byte[1024 * 4];
                var messageBuilder = new StringBuilder();

                while (webSocket.State == WebSocketState.Open)
                {
                    var result = await webSocket.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);

                    if (result.MessageType == WebSocketMessageType.Close)
                    {
                        _connectedClients.TryRemove(clientId, out _);
                        if (webSocket.State == WebSocketState.Open)
                        {
                            await webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Closed by client", CancellationToken.None);
                        }
                        break;
                    }
                    else
                    {
                        messageBuilder.Append(Encoding.UTF8.GetString(buffer, 0, result.Count));
                        if (result.EndOfMessage)
                        {
                            var message = messageBuilder.ToString();
                            messageBuilder.Clear();

                            await BroadcastMessageAsync($"Client {clientId}: {message}");
                        }
                        Console.WriteLine("~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~socket connection open~~~~~~~~~~~~~~~~~~");
                    }
					Console.WriteLine("**************************socket connection open*******************************");
				}
            }
            catch (WebSocketException ex)
            {
                Console.WriteLine($"WebSocket error: {ex.Message}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Unexpected error: {ex.Message}");
            }
            finally
            {
                _connectedClients.TryRemove(clientId, out _);
                if (webSocket.State != WebSocketState.Closed)
                {
                    await webSocket.CloseAsync(WebSocketCloseStatus.InternalServerError, "Closed by server", CancellationToken.None);
                }
            }
        }

        public static async Task BroadcastMessageAsync(string message)
        {
            var messageBuffer = Encoding.UTF8.GetBytes(message);

            foreach (var webSocket in _connectedClients.Values)
            {
                if (webSocket.State == WebSocketState.Open)
                {
                    await webSocket.SendAsync(new ArraySegment<byte>(messageBuffer), WebSocketMessageType.Text, true, CancellationToken.None);
                }
            }
        }

        public static async Task SendMailCountInfo(MailInfo pInfo)
        {
            var objPacket = new
            {
                MailInfo = pInfo,
                type = "mail"
            };
            string strJson = System.Text.Json.JsonSerializer.Serialize(objPacket);
            await BroadcastMessageAsync(strJson);
        }

       
        public static async Task SendNewRequestCnt(string pInfo)
        {
            var objPacket = new
            {
                order = pInfo,
                type = "new_order"
            };
            await BroadcastMessageAsync(System.Text.Json.JsonSerializer.Serialize(objPacket));
        }

        public static async Task SendNewRequest(string strJson)
        {
            var objPacket = new
            {
                order = strJson,
                type = "new_order"
            };
            await BroadcastMessageAsync(System.Text.Json.JsonSerializer.Serialize(objPacket));
        }
    }

    public class OrderRequest
    {
        public string store { set; get; }
        public int count { set; get; }
        public string token { set; get; }
    }

    public class MailInfo
    {
        public int nCntWhole { set; get; }
        public int nCntRead { set; get; }
        public int nCntUnread { set; get; }
        public int nCntLate { set; get; }
        public int nCntDanger { set; get; }
        public int nCntOnTime { set; get; }
        public int nCntArchived { set; get; }
        public int nCntReply { set; get; }
        public MailInfo()
        {
            this.nCntWhole = 0;
            this.nCntRead = 0;
            this.nCntUnread = 0;
            this.nCntLate = 0;
            this.nCntDanger = 0;
            this.nCntOnTime = 0;
            this.nCntArchived = 0;
            this.nCntReply = 0;
        }

        public MailInfo(int nCntWhole, int nCntRead, int nCntLate, int nCntDanger, int nCntOnTime, int nCntUnred)
        {
            this.nCntWhole = nCntWhole;
            this.nCntRead = nCntRead;
            this.nCntLate = nCntLate;
            this.nCntDanger = nCntDanger;
            this.nCntOnTime = nCntOnTime;
            this.nCntUnread = nCntUnread;
        }
    }
}
