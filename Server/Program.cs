using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using HardcoreMessenger.Shared;

namespace HardcoreServer
{
    class Program
    {
        static async Task Main(string[] args)
        {
            var server = new HardcoreWebSocketServer();
            await server.Start();
        }
    }

    public class HardcoreWebSocketServer
    {
        private HttpListener _httpListener;
        private ConcurrentDictionary<string, ClientConnection> _clients = new ConcurrentDictionary<string, ClientConnection>();
        private ConcurrentDictionary<string, List<Message>> _messageHistory = new ConcurrentDictionary<string, List<Message>>();
        private ConcurrentDictionary<string, ProfileData> _userProfiles = new ConcurrentDictionary<string, ProfileData>();

        public async Task Start()
        {
            // Определяем порт (для Heroku/Railway используют переменную окружения PORT)
            var port = Environment.GetEnvironmentVariable("PORT") ?? "8080";
            
            _httpListener = new HttpListener();
            // ВАЖНО: Слушаем ВСЕ IP адреса (не только localhost!)
            _httpListener.Prefixes.Add($"http://+:{port}/");
            
            try
            {
                _httpListener.Start();
            }
            catch (HttpListenerException ex)
            {
                Console.WriteLine($"[ERROR] Cannot start server on port {port}");
                Console.WriteLine($"[ERROR] {ex.Message}");
                Console.WriteLine("\nTry running as Administrator or use a different port.");
                Console.ReadKey();
                return;
            }
            
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine(@"
    ╦ ╦╔═╗╦═╗╔╦╗╔═╗╔═╗╦═╗╔═╗  ╔═╗╔═╗╦═╗╦  ╦╔═╗╦═╗
    ╠═╣╠═╣╠╦╝ ║║║  ║ ║╠╦╝║╣   ╚═╗║╣ ╠╦╝╚╗╔╝║╣ ╠╦╝
    ╩ ╩╩ ╩╩╚══╩╝╚═╝╚═╝╩╚═╚═╝  ╚═╝╚═╝╩╚═ ╚╝ ╚═╝╩╚═
    
    🌐 ONLINE EDITION - Доступен из интернета!
            ");
            Console.ResetColor();
            
            Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] 🚀 Server started on port {port}");
            Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] 🌐 Listening on ALL network interfaces");
            Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] 📡 Waiting for connections...\n");
            
            // Показываем IP адреса
            ShowNetworkInfo(port);

            while (true)
            {
                try
                {
                    var context = await _httpListener.GetContextAsync();
                    if (context.Request.IsWebSocketRequest)
                    {
                        _ = ProcessWebSocketRequest(context);
                    }
                    else
                    {
                        context.Response.StatusCode = 400;
                        context.Response.Close();
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[ERROR] {ex.Message}");
                }
            }
        }

        private void ShowNetworkInfo(string port)
        {
            try
            {
                var hostName = Dns.GetHostName();
                var addresses = Dns.GetHostAddresses(hostName);
                
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine("📍 Ваш сервер доступен по адресам:");
                Console.ResetColor();
                
                Console.WriteLine($"   Локально:  ws://localhost:{port}");
                
                foreach (var addr in addresses.Where(a => a.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork))
                {
                    Console.WriteLine($"   Локальная сеть: ws://{addr}:{port}");
                }
                
                Console.WriteLine("\n💡 Для доступа из интернета используйте:");
                Console.WriteLine("   - ngrok: ngrok http " + port);
                Console.WriteLine("   - Railway.app (автоматический публичный URL)");
                Console.WriteLine("   - Ваш публичный IP + проброс портов\n");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[WARNING] Cannot get network info: {ex.Message}");
            }
        }

        private async Task ProcessWebSocketRequest(HttpListenerContext context)
        {
            WebSocketContext wsContext = null;
            try
            {
                wsContext = await context.AcceptWebSocketAsync(null);
                var webSocket = wsContext.WebSocket;
                var username = context.Request.QueryString["username"] ?? $"User{_clients.Count + 1}";

                var client = new ClientConnection
                {
                    Username = username,
                    WebSocket = webSocket,
                    Id = Guid.NewGuid().ToString(),
                    IPAddress = context.Request.RemoteEndPoint?.Address.ToString()
                };

                _clients.TryAdd(client.Id, client);
                
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] ✓ {username} connected from {client.IPAddress} (Total: {_clients.Count})");
                Console.ResetColor();

                // Отправляем профили всех пользователей новому клиенту
                await SendUserProfiles(client);
                
                // Обновляем список пользователей для всех
                await BroadcastUserList();
                
                await ReceiveMessages(client);
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] ✗ Connection error: {ex.Message}");
                Console.ResetColor();
            }
        }

        private async Task SendUserProfiles(ClientConnection newClient)
        {
            foreach (var profile in _userProfiles.Values)
            {
                var profileMsg = new Message
                {
                    Type = MessageType.ProfileUpdate,
                    From = "System",
                    To = newClient.Username,
                    Content = JsonConvert.SerializeObject(profile)
                };
                await SendToClient(newClient, profileMsg);
            }
        }

        private async Task ReceiveMessages(ClientConnection client)
        {
            var buffer = new byte[1024 * 16]; // 16KB буфер для изображений
            
            try
            {
                while (client.WebSocket.State == WebSocketState.Open)
                {
                    var result = await client.WebSocket.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);
                    
                    if (result.MessageType == WebSocketMessageType.Close)
                    {
                        await HandleDisconnect(client);
                        break;
                    }

                    var messageJson = Encoding.UTF8.GetString(buffer, 0, result.Count);
                    var message = JsonConvert.DeserializeObject<Message>(messageJson);

                    if (message != null)
                    {
                        message.From = client.Username;
                        message.Timestamp = DateTime.Now;

                        Console.ForegroundColor = ConsoleColor.Yellow;
                        Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] 📨 {message.From} → {message.To}: {message.Type}");
                        Console.ResetColor();

                        await RouteMessage(message, client);
                        
                        // Сохраняем в историю (кроме служебных)
                        if (message.Type == MessageType.Text)
                        {
                            var key = GetChatKey(message.From, message.To);
                            if (!_messageHistory.ContainsKey(key))
                                _messageHistory[key] = new List<Message>();
                            _messageHistory[key].Add(message);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] {client.Username}: {ex.Message}");
                await HandleDisconnect(client);
            }
        }

        private async Task RouteMessage(Message message, ClientConnection sender)
        {
            switch (message.Type)
            {
                case MessageType.Text:
                    await SendToUser(message.To, message);
                    // Подтверждение
                    var deliveryConfirm = new Message
                    {
                        Type = MessageType.Delivered,
                        From = "System",
                        To = message.From,
                        Content = message.Id
                    };
                    await SendToUser(message.From, deliveryConfirm);
                    break;

                case MessageType.Typing:
                    await SendToUser(message.To, message);
                    break;

                case MessageType.Read:
                    await SendToUser(message.To, message);
                    break;

                case MessageType.ProfileUpdate:
                case MessageType.AvatarUpdate:
                    // Сохраняем профиль
                    var profile = JsonConvert.DeserializeObject<ProfileData>(message.Content);
                    _userProfiles[message.From] = profile;
                    
                    // Рассылаем всем
                    await BroadcastProfileUpdate(message);
                    break;

                case MessageType.StatusUpdate:
                    // Обновление статуса
                    await BroadcastMessage(message);
                    break;

                case MessageType.Reaction:
                    // Реакция на сообщение
                    await SendToUser(message.To, message);
                    break;
            }
        }

        private async Task SendToUser(string username, Message message)
        {
            var client = _clients.Values.FirstOrDefault(c => c.Username == username);
            if (client != null)
            {
                await SendToClient(client, message);
            }
        }

        private async Task SendToClient(ClientConnection client, Message message)
        {
            if (client.WebSocket.State == WebSocketState.Open)
            {
                try
                {
                    var json = JsonConvert.SerializeObject(message);
                    var bytes = Encoding.UTF8.GetBytes(json);
                    await client.WebSocket.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, CancellationToken.None);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[ERROR] Cannot send to {client.Username}: {ex.Message}");
                }
            }
        }

        private async Task BroadcastProfileUpdate(Message message)
        {
            foreach (var client in _clients.Values)
            {
                await SendToClient(client, message);
            }
        }

        private async Task BroadcastMessage(Message message)
        {
            var json = JsonConvert.SerializeObject(message);
            var bytes = Encoding.UTF8.GetBytes(json);

            foreach (var client in _clients.Values)
            {
                if (client.WebSocket.State == WebSocketState.Open)
                {
                    try
                    {
                        await client.WebSocket.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, CancellationToken.None);
                    }
                    catch { }
                }
            }
        }

        private async Task BroadcastUserList()
        {
            var userList = _clients.Values.Select(c =>
            {
                ProfileData profile = null;
                _userProfiles.TryGetValue(c.Username, out profile);
                
                return new User
                {
                    Username = c.Username,
                    Status = UserStatus.Online,
                    LastSeen = DateTime.Now,
                    Avatar = profile?.Avatar ?? c.Username.Substring(0, 1).ToUpper(),
                    AvatarType = profile?.AvatarType ?? "emoji",
                    Bio = profile?.Bio,
                    CustomStatus = profile?.CustomStatus
                };
            }).ToList();

            var message = new Message
            {
                Type = MessageType.UserList,
                From = "System",
                Content = JsonConvert.SerializeObject(userList)
            };

            await BroadcastMessage(message);
        }

        private async Task HandleDisconnect(ClientConnection client)
        {
            _clients.TryRemove(client.Id, out _);
            
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] ✗ {client.Username} disconnected from {client.IPAddress} (Total: {_clients.Count})");
            Console.ResetColor();

            await BroadcastUserList();
            
            if (client.WebSocket.State == WebSocketState.Open)
            {
                await client.WebSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Disconnected", CancellationToken.None);
            }
        }

        private string GetChatKey(string user1, string user2)
        {
            var users = new[] { user1, user2 }.OrderBy(u => u);
            return string.Join("_", users);
        }
    }

    public class ClientConnection
    {
        public string Id { get; set; }
        public string Username { get; set; }
        public WebSocket WebSocket { get; set; }
        public DateTime ConnectedAt { get; set; } = DateTime.Now;
        public string IPAddress { get; set; }
    }
}
