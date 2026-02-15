using System;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using Newtonsoft.Json;
using HardcoreMessenger.Shared;

namespace HardcoreClient
{
    public partial class LoginWindow : Window
    {
        // ============================================
        // 🌐 НАСТРОЙКА СЕРВЕРА - ИЗМЕНИТЕ ЗДЕСЬ!
        // ============================================
        
        // Для Railway.app:
        private const string SERVER_URL = "hardcore-messenger228-production.up.railway.app";
        
        // Для Render.com:
        // private const string SERVER_URL = "wss://hardcore-messenger.onrender.com";
        
        // Для локального тестирования:
        // private const string SERVER_URL = "ws://localhost:8080";
        
        // ============================================

        private ClientWebSocket _webSocket;
        private bool _isRegisterMode = false;

        public string AuthenticatedUsername { get; private set; }
        public ClientWebSocket AuthenticatedSocket { get; private set; }

        public LoginWindow()
        {
            InitializeComponent();
            TxtServerInfo.Text = $"Сервер: {SERVER_URL.Replace("wss://", "").Replace("ws://", "")}";
        }

        private void RadioLogin_Checked(object sender, RoutedEventArgs e)
        {
            _isRegisterMode = false;
            BtnSubmit.Content = "ВОЙТИ";
            TxtStatus.Visibility = Visibility.Collapsed;
        }

        private void RadioRegister_Checked(object sender, RoutedEventArgs e)
        {
            _isRegisterMode = true;
            BtnSubmit.Content = "ЗАРЕГИСТРИРОВАТЬСЯ";
            TxtStatus.Visibility = Visibility.Collapsed;
        }

        private async void BtnSubmit_Click(object sender, RoutedEventArgs e)
        {
            string username = TxtUsername.Text.Trim();
            string password = TxtPassword.Password.Trim();

            // Валидация
            if (string.IsNullOrWhiteSpace(username))
            {
                ShowError("Введите имя пользователя!");
                return;
            }

            if (string.IsNullOrWhiteSpace(password))
            {
                ShowError("Введите пароль!");
                return;
            }

            if (username.Length < 3)
            {
                ShowError("Имя пользователя должно быть не менее 3 символов!");
                return;
            }

            if (password.Length < 6)
            {
                ShowError("Пароль должен быть не менее 6 символов!");
                return;
            }

            // Блокируем UI
            BtnSubmit.IsEnabled = false;
            TxtUsername.IsEnabled = false;
            TxtPassword.IsEnabled = false;
            RadioLogin.IsEnabled = false;
            RadioRegister.IsEnabled = false;
            LoadingBar.Visibility = Visibility.Visible;
            TxtStatus.Visibility = Visibility.Collapsed;

            try
            {
                if (_isRegisterMode)
                {
                    await RegisterUser(username, password);
                }
                else
                {
                    await LoginUser(username, password);
                }
            }
            catch (Exception ex)
            {
                ShowError($"Ошибка подключения: {ex.Message}");
                
                // Разблокируем UI
                BtnSubmit.IsEnabled = true;
                TxtUsername.IsEnabled = true;
                TxtPassword.IsEnabled = true;
                RadioLogin.IsEnabled = true;
                RadioRegister.IsEnabled = true;
                LoadingBar.Visibility = Visibility.Collapsed;
            }
        }

        private async Task RegisterUser(string username, string password)
        {
            TxtServerInfo.Text = "Подключение к серверу...";

            // Создаём WebSocket подключение
            _webSocket = new ClientWebSocket();
            await _webSocket.ConnectAsync(new Uri(SERVER_URL), CancellationToken.None);

            TxtServerInfo.Text = "Отправка запроса на регистрацию...";

            // Отправляем запрос на регистрацию
            var registerMessage = new Message
            {
                Type = MessageType.Register,
                From = username,
                Content = password
            };

            await SendMessage(registerMessage);

            // Ждём ответ от сервера
            var response = await ReceiveMessage();

            if (response.Type == MessageType.Register)
            {
                if (response.Content == "SUCCESS")
                {
                    // Регистрация успешна!
                    ShowSuccess("Регистрация успешна! Вход в систему...");
                    await Task.Delay(1000);

                    // Сохраняем данные для главного окна
                    AuthenticatedUsername = username;
                    AuthenticatedSocket = _webSocket;

                    // Закрываем окно входа с успехом
                    DialogResult = true;
                    Close();
                }
                else if (response.Content == "ERROR:USERNAME_EXISTS")
                {
                    ShowError("Это имя пользователя уже занято!");
                    _webSocket.Dispose();
                    BtnSubmit.IsEnabled = true;
                    TxtUsername.IsEnabled = true;
                    TxtPassword.IsEnabled = true;
                    RadioLogin.IsEnabled = true;
                    RadioRegister.IsEnabled = true;
                    LoadingBar.Visibility = Visibility.Collapsed;
                }
                else
                {
                    ShowError($"Ошибка регистрации: {response.Content}");
                    _webSocket.Dispose();
                    BtnSubmit.IsEnabled = true;
                    TxtUsername.IsEnabled = true;
                    TxtPassword.IsEnabled = true;
                    RadioLogin.IsEnabled = true;
                    RadioRegister.IsEnabled = true;
                    LoadingBar.Visibility = Visibility.Collapsed;
                }
            }
        }

        private async Task LoginUser(string username, string password)
        {
            TxtServerInfo.Text = "Подключение к серверу...";

            // Создаём WebSocket подключение
            _webSocket = new ClientWebSocket();
            await _webSocket.ConnectAsync(new Uri(SERVER_URL), CancellationToken.None);

            TxtServerInfo.Text = "Проверка учётных данных...";

            // Отправляем запрос на вход
            var loginMessage = new Message
            {
                Type = MessageType.LoginAttempt,
                From = username,
                Content = password
            };

            await SendMessage(loginMessage);

            // Ждём ответ от сервера
            var response = await ReceiveMessage();

            if (response.Type == MessageType.LoginAttempt)
            {
                if (response.Content == "SUCCESS")
                {
                    // Вход успешен!
                    ShowSuccess("Вход выполнен!");
                    await Task.Delay(500);

                    // Сохраняем данные для главного окна
                    AuthenticatedUsername = username;
                    AuthenticatedSocket = _webSocket;

                    // Закрываем окно входа с успехом
                    DialogResult = true;
                    Close();
                }
                else if (response.Content == "ERROR:INVALID_CREDENTIALS")
                {
                    ShowError("Неверное имя пользователя или пароль!");
                    _webSocket.Dispose();
                    BtnSubmit.IsEnabled = true;
                    TxtUsername.IsEnabled = true;
                    TxtPassword.IsEnabled = true;
                    RadioLogin.IsEnabled = true;
                    RadioRegister.IsEnabled = true;
                    LoadingBar.Visibility = Visibility.Collapsed;
                }
                else
                {
                    ShowError($"Ошибка входа: {response.Content}");
                    _webSocket.Dispose();
                    BtnSubmit.IsEnabled = true;
                    TxtUsername.IsEnabled = true;
                    TxtPassword.IsEnabled = true;
                    RadioLogin.IsEnabled = true;
                    RadioRegister.IsEnabled = true;
                    LoadingBar.Visibility = Visibility.Collapsed;
                }
            }
        }

        private async Task SendMessage(Message message)
        {
            string json = JsonConvert.SerializeObject(message);
            byte[] bytes = Encoding.UTF8.GetBytes(json);
            await _webSocket.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, CancellationToken.None);
        }

        private async Task<Message> ReceiveMessage()
        {
            byte[] buffer = new byte[1024 * 16];
            var result = await _webSocket.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);
            string json = Encoding.UTF8.GetString(buffer, 0, result.Count);
            return JsonConvert.DeserializeObject<Message>(json);
        }

        private void ShowError(string message)
        {
            TxtStatus.Text = "❌ " + message;
            TxtStatus.Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(255, 85, 85));
            TxtStatus.Visibility = Visibility.Visible;
        }

        private void ShowSuccess(string message)
        {
            TxtStatus.Text = "✅ " + message;
            TxtStatus.Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(85, 255, 85));
            TxtStatus.Visibility = Visibility.Visible;
        }
    }
}
