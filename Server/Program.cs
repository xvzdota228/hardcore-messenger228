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
        private DatabasePostgres _database;

        public async Task Start()
        {
            // Инициализация базы данных PostgreSQL
            // Railway автоматически создаёт переменную DATABASE_URL
            string connectionString = Environment.GetEnvironmentVariable("DATABASE_URL") 
                ?? throw new Exception("DATABASE_URL environment variable not set!");
            
            _database = new DatabasePostgres(connectionString);
            
            Console.WriteLine("[SERVER] ✓ PostgreSQL database connected");

            // Определяем порт
            var port = Environment.GetEnvironmentVariable("PORT") ?? "8080";
            
            _httpListener = new HttpListener();
            _httpListener.Prefixes.Add($"http://+:{port}/");
            
            try
            {
                _httpListener.Start();
            }
            catch (HttpListenerException ex)
            {
                Console.WriteLine($"[ERROR] Cannot start server on port {port}");
                Console.WriteLine($"[ERROR] {ex.Message}");
                Console.ReadKey();
                return;
            }
            
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine(@"
    ╦ ╦╔═╗╦═╗╔╦╗╔═╗╔═╗╦═╗╔═╗  ╔═╗╔═╗╦═╗╦  ╦╔═╗╦═╗
    ╠═╣╠═╣╠╦╝ ║║║  ║ ║╠╦╝║╣   ╚═╗║╣ ╠╦╝╚╗╔╝║╣ ╠╦╝
    ╩ ╩╩ ╩╩╚══╩╝╚═╝╚═╝╩╚═╚═╝  ╚═╝╚═╝╩╚═ ╚╝ ╚═╝╩╚═
    
    🌐 ONLINE EDITION v2.0 - С РЕГИСТРАЦИЕЙ И БД!
            ");
            Console.ResetColor();
            
            Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] 🚀 Server started on port {port}");
            Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] 💾 Database: PostgreSQL");
            Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] 📡 Waiting for connections...\n");

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

        private async Task ProcessWebSocketRequest(HttpListenerContext context)
        {
            WebSocketContext wsContext = null;
            try
            {
                wsContext = await context.AcceptWebSocketAsync(null);
                var webSocket = wsContext.WebSocket;
                var tempId = Guid.NewGuid().ToString();

                var client = new ClientConnection
                {
                    Username = null, // Будет установлен после авторизации
                    WebSocket = webSocket,
                    Id = tempId,
                    IPAddress = context.Request.RemoteEndPoint?.Address.ToString(),
                    IsAuthenticated = false
                };

                _clients.TryAdd(client.Id, client);
                
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] 🔌 New connection from {client.IPAddress} (waiting for auth...)");
                Console.ResetColor();

                await ReceiveMessages(client);
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] ✗ Connection error: {ex.Message}");
                Console.ResetColor();
            }
        }

        private async Task ReceiveMessages(ClientConnection client)
        {
            var buffer = new byte[1024 * 16];
            
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
                        // Обработка регистрации и входа (до авторизации)
                        if (!client.IsAuthenticated)
                        {
                            if (message.Type == MessageType.Register)
                            {
                                await HandleRegistration(client, message);
                                continue;
                            }
                            else if (message.Type == MessageType.LoginAttempt)
                            {
                                await HandleLogin(client, message);
                                continue;
                            }
                            else
                            {
                                // Неавторизованный пользователь пытается что-то сделать
                                var errorMsg = new Message
                                {
                                    Type = MessageType.LoginAttempt,
                                    From = "System",
                                    Content = "ERROR:NOT_AUTHENTICATED"
                                };
                                await SendToClient(client, errorMsg);
                                continue;
                            }
                        }

                        // Дальше только для авторизованных пользователей
                        message.From = client.Username;
                        message.Timestamp = DateTime.Now;

                        Console.ForegroundColor = ConsoleColor.Yellow;
                        Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] 📨 {message.From} → {message.To}: {message.Type}");
                        Console.ResetColor();

                        await RouteMessage(message, client);
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] {client.Username ?? client.Id}: {ex.Message}");
                await HandleDisconnect(client);
            }
        }

        private async Task HandleRegistration(ClientConnection client, Message message)
        {
            string username = message.From;
            string password = message.Content;

            Console.WriteLine($"[AUTH] 📝 Registration attempt: {username}");

            if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
            {
                var response = new Message
                {
                    Type = MessageType.Register,
                    From = "System",
                    Content = "ERROR:INVALID_INPUT"
                };
                await SendToClient(client, response);
                return;
            }

            bool success = _database.RegisterUser(username, password);

            if (success)
            {
                // Регистрация успешна - автоматически логиним
                client.Username = username;
                client.IsAuthenticated = true;
                
                _database.CreateSession(client.Id, username, client.IPAddress);

                var response = new Message
                {
                    Type = MessageType.Register,
                    From = "System",
                    Content = "SUCCESS"
                };
                await SendToClient(client, response);

                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine($"[AUTH] ✓ User registered and logged in: {username}");
                Console.ResetColor();

                // Отправляем профиль пользователя
                var profile = _database.GetUserProfile(username);
                var profileMsg = new Message
                {
                    Type = MessageType.ProfileUpdate,
                    From = "System",
                    Content = JsonConvert.SerializeObject(profile)
                };
                await SendToClient(client, profileMsg);

                // Отправляем список пользователей
                await SendUserList(client);

                // Уведомляем всех о новом пользователе
                await BroadcastUserList();
            }
            else
            {
                var response = new Message
                {
                    Type = MessageType.Register,
                    From = "System",
                    Content = "ERROR:USERNAME_EXISTS"
                };
                await SendToClient(client, response);

                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"[AUTH] ✗ Registration failed: {username} (already exists)");
                Console.ResetColor();
            }
        }

        private async Task HandleLogin(ClientConnection client, Message message)
        {
            string username = message.From;
            string password = message.Content;

            Console.WriteLine($"[AUTH] 🔐 Login attempt: {username}");

            if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
            {
                var response = new Message
                {
                    Type = MessageType.LoginAttempt,
                    From = "System",
                    Content = "ERROR:INVALID_INPUT"
                };
                await SendToClient(client, response);
                return;
            }

            bool success = _database.LoginUser(username, password);

            if (success)
            {
                client.Username = username;
                client.IsAuthenticated = true;
                
                _database.CreateSession(client.Id, username, client.IPAddress);

                var response = new Message
                {
                    Type = MessageType.LoginAttempt,
                    From = "System",
                    Content = "SUCCESS"
                };
                await SendToClient(client, response);

                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine($"[AUTH] ✓ User logged in: {username} from {client.IPAddress}");
                Console.ResetColor();

                // Отправляем профиль
                var profile = _database.GetUserProfile(username);
                var profileMsg = new Message
                {
                    Type = MessageType.ProfileUpdate,
                    From = "System",
                    Content = JsonConvert.SerializeObject(profile)
                };
                await SendToClient(client, profileMsg);

                // Отправляем список пользователей
                await SendUserList(client);

                // Уведомляем всех об онлайне
                await BroadcastUserList();
            }
            else
            {
                var response = new Message
                {
                    Type = MessageType.LoginAttempt,
                    From = "System",
                    Content = "ERROR:INVALID_CREDENTIALS"
                };
                await SendToClient(client, response);

                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"[AUTH] ✗ Login failed: {username} (invalid credentials)");
                Console.ResetColor();
            }
        }

        private async Task RouteMessage(Message message, ClientConnection sender)
        {
            switch (message.Type)
            {
                case MessageType.Text:
                    // Сохраняем в БД
                    _database.SaveMessage(message);
                    
                    // Отправляем получателю
                    await SendToUser(message.To, message);
                    
                    // Подтверждение отправителю
                    var deliveryConfirm = new Message
                    {
                        Type = MessageType.Delivered,
                        From = "System",
                        To = message.From,
                        Content = message.Id
                    };
                    await SendToUser(message.From, deliveryConfirm);
                    break;

                case MessageType.GetHistory:
                    // Клиент запрашивает историю с пользователем
                    var history = _database.GetMessageHistory(message.From, message.To, 100);
                    var historyMsg = new Message
                    {
                        Type = MessageType.History,
                        From = "System",
                        To = message.From,
                        Content = JsonConvert.SerializeObject(history)
                    };
                    await SendToClient(sender, historyMsg);
                    break;

                case MessageType.Read:
                    _database.MarkMessagesAsRead(message.To, message.From);
                    await SendToUser(message.To, message);
                    break;

                case MessageType.Typing:
                    await SendToUser(message.To, message);
                    break;

                case MessageType.ProfileUpdate:
                case MessageType.AvatarUpdate:
                    var profileData = JsonConvert.DeserializeObject<ProfileData>(message.Content);
                    _database.UpdateUserProfile(profileData);
                    await BroadcastProfileUpdate(profileData);
                    break;

                default:
                    await SendToUser(message.To, message);
                    break;
            }
        }

        private async Task SendUserList(ClientConnection client)
        {
            var onlineUsers = _clients.Values
                .Where(c => c.IsAuthenticated && c.Username != null)
                .Select(c => new User
                {
                    Username = c.Username,
                    Status = UserStatus.Online,
                    Avatar = _database.GetUserProfile(c.Username)?.Avatar ?? c.Username.Substring(0, 1).ToUpper(),
                    AvatarType = "emoji"
                })
                .ToList();

            var userListMsg = new Message
            {
                Type = MessageType.UserList,
                From = "System",
                To = client.Username,
                Content = JsonConvert.SerializeObject(onlineUsers)
            };

            await SendToClient(client, userListMsg);
        }

        private async Task BroadcastUserList()
        {
            var onlineUsers = _clients.Values
                .Where(c => c.IsAuthenticated && c.Username != null)
                .Select(c => new User
                {
                    Username = c.Username,
                    Status = UserStatus.Online,
                    Avatar = _database.GetUserProfile(c.Username)?.Avatar ?? c.Username.Substring(0, 1).ToUpper(),
                    AvatarType = "emoji"
                })
                .ToList();

            var userListMsg = new Message
            {
                Type = MessageType.UserList,
                From = "System",
                Content = JsonConvert.SerializeObject(onlineUsers)
            };

            foreach (var client in _clients.Values.Where(c => c.IsAuthenticated))
            {
                await SendToClient(client, userListMsg);
            }
        }

        private async Task BroadcastProfileUpdate(ProfileData profile)
        {
            var profileMsg = new Message
            {
                Type = MessageType.ProfileUpdate,
                From = "System",
                Content = JsonConvert.SerializeObject(profile)
            };

            foreach (var client in _clients.Values.Where(c => c.IsAuthenticated))
            {
                await SendToClient(client, profileMsg);
            }
        }

        private async Task SendToUser(string username, Message message)
        {
            var client = _clients.Values.FirstOrDefault(c => c.Username == username && c.IsAuthenticated);
            
            if (client != null)
            {
                await SendToClient(client, message);
            }
        }

        private async Task SendToClient(ClientConnection client, Message message)
        {
            try
            {
                if (client.WebSocket.State == WebSocketState.Open)
                {
                    var json = JsonConvert.SerializeObject(message);
                    var bytes = Encoding.UTF8.GetBytes(json);
                    await client.WebSocket.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, CancellationToken.None);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] Cannot send to {client.Username ?? client.Id}: {ex.Message}");
            }
        }

        private async Task HandleDisconnect(ClientConnection client)
        {
            _clients.TryRemove(client.Id, out _);
            
            if (client.IsAuthenticated)
            {
                _database.RemoveSession(client.Id);
                
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] ✗ {client.Username} disconnected (Total: {_clients.Count})");
                Console.ResetColor();

                await BroadcastUserList();
            }

            try
            {
                await client.WebSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Disconnected", CancellationToken.None);
            }
            catch { }
        }
    }

    public class ClientConnection
    {
        public string Id { get; set; }
        public string Username { get; set; }
        public WebSocket WebSocket { get; set; }
        public string IPAddress { get; set; }
        public bool IsAuthenticated { get; set; }
    }
}
