using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using Newtonsoft.Json;
using HardcoreMessenger.Shared;

namespace HardcoreMessenger
{
    public partial class MainWindow : Window
    {
        // ============================================
        // 🌐 НАСТРОЙКА СЕРВЕРА - ИЗМЕНИТЕ ЗДЕСЬ!
        // ============================================
        
        // Для ЛОКАЛЬНОГО тестирования:
        private const string SERVER_URL = "ws://localhost:8080";
        
        // Для ngrok (после запуска ngrok http 8080):
        // private const string SERVER_URL = "wss://ВАШ-АДРЕС.ngrok.io";
        
        // Для Railway.app:
        // private const string SERVER_URL = "wss://hardcore-messenger.up.railway.app";
        
        // Для Render.com:
        // private const string SERVER_URL = "wss://hardcore-messenger.onrender.com";
        
        // Для вашего домашнего сервера (замените IP):
        // private const string SERVER_URL = "ws://45.123.67.89:8080";
        
        // ============================================

        private ClientWebSocket _webSocket;
        private string _username;
        private string _currentChatUser;
        private ObservableCollection<UserViewModel> _users = new ObservableCollection<UserViewModel>();
        private ObservableCollection<MessageViewModel> _messages = new ObservableCollection<MessageViewModel>();
        private CancellationTokenSource _cancellationTokenSource;
        private bool _isTyping = false;
        private System.Windows.Threading.DispatcherTimer _typingTimer;

        // НОВЫЙ КОНСТРУКТОР - принимает username и уже подключённый WebSocket
        public MainWindow(string username, ClientWebSocket webSocket)
        {
            InitializeComponent();
            UsersList.ItemsSource = _users;
            MessagesPanel.ItemsSource = _messages;

            // Сохраняем данные из окна входа
            _username = username;
            _webSocket = webSocket;
            _cancellationTokenSource = new CancellationTokenSource();

            _typingTimer = new System.Windows.Threading.DispatcherTimer();
            _typingTimer.Interval = TimeSpan.FromSeconds(2);
            _typingTimer.Tick += (s, e) =>
            {
                _isTyping = false;
                _typingTimer.Stop();
            };

            // Показываем что мы подключены
            StatusText.Text = $"● Connected as {_username}";
            StatusText.Foreground = new SolidColorBrush(Color.FromRgb(61, 237, 151));

            // Прячем панель входа, показываем чат
            LoginPanel.Visibility = Visibility.Collapsed;
            ChatPanel.Visibility = Visibility.Visible;

            // Запускаем прослушивание сообщений
            _ = ReceiveMessages();
        }

        private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed)
                DragMove();
        }

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            _cancellationTokenSource?.Cancel();
            _webSocket?.Dispose();
            Application.Current.Shutdown();
        }

        private void Minimize_Click(object sender, RoutedEventArgs e)
        {
            WindowState = WindowState.Minimized;
        }

        private void Maximize_Click(object sender, RoutedEventArgs e)
        {
            WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
        }

        // СТАРЫЕ ФУНКЦИИ Login_Click, UsernameBox_KeyDown и ConnectToServer УДАЛЕНЫ
        // Теперь вход происходит через LoginWindow!

        private async Task ReceiveMessages()
        {
            var buffer = new byte[8192];

            try
            {
                while (_webSocket.State == WebSocketState.Open && !_cancellationTokenSource.Token.IsCancellationRequested)
                {
                    var result = await _webSocket.ReceiveAsync(new ArraySegment<byte>(buffer), _cancellationTokenSource.Token);
                    
                    if (result.MessageType == WebSocketMessageType.Close)
                    {
                        await _webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "", CancellationToken.None);
                        break;
                    }

                    var messageJson = Encoding.UTF8.GetString(buffer, 0, result.Count);
                    var message = JsonConvert.DeserializeObject<Message>(messageJson);

                    Dispatcher.Invoke(() => HandleIncomingMessage(message));
                }
            }
            catch (Exception ex)
            {
                if (!_cancellationTokenSource.Token.IsCancellationRequested)
                {
                    Dispatcher.Invoke(() =>
                    {
                        StatusText.Text = "● Disconnected";
                        StatusText.Foreground = new SolidColorBrush(Color.FromRgb(229, 57, 53));
                        MessageBox.Show($"Connection lost: {ex.Message}", "HARDCORE", MessageBoxButton.OK, MessageBoxImage.Error);
                    });
                }
            }
        }

        private void HandleIncomingMessage(Message message)
        {
            switch (message.Type)
            {
                case MessageType.UserList:
                    var users = JsonConvert.DeserializeObject<System.Collections.Generic.List<User>>(message.Content);
                    _users.Clear();
                    foreach (var user in users.Where(u => u.Username != _username))
                    {
                        _users.Add(new UserViewModel
                        {
                            Username = user.Username,
                            Avatar = user.Username.Substring(0, 1).ToUpper(),
                            StatusText = "online",
                            OnlineVisibility = Visibility.Visible
                        });
                    }
                    break;

                case MessageType.Text:
                    if (message.From == _currentChatUser || message.To == _currentChatUser)
                    {
                        AddMessageToChat(message);
                    }
                    break;

                case MessageType.Typing:
                    if (message.From == _currentChatUser)
                    {
                        ChatStatus.Text = "typing...";
                    }
                    break;

                case MessageType.Delivered:
                    UpdateMessageStatus(message.Content, "✓✓");
                    break;
            }
        }

        private void AddMessageToChat(Message message)
        {
            var isMyMessage = message.From == _username;
            
            var msgViewModel = new MessageViewModel
            {
                Content = message.Content,
                TimeString = message.Timestamp.ToString("HH:mm"),
                Alignment = isMyMessage ? HorizontalAlignment.Right : HorizontalAlignment.Left,
                BubbleColor = isMyMessage ? 
                    new SolidColorBrush(Color.FromRgb(43, 82, 120)) : 
                    new SolidColorBrush(Color.FromRgb(24, 37, 51)),
                MessageId = message.Id
            };

            _messages.Add(msgViewModel);
            MessagesScroll.ScrollToEnd();
            AnimateNewMessage();
        }

        private void AnimateNewMessage()
        {
            if (_messages.Count > 0)
            {
                var lastIndex = _messages.Count - 1;
            }
        }

        private void UpdateMessageStatus(string messageId, string status)
        {
            var msg = _messages.FirstOrDefault(m => m.MessageId == messageId);
            if (msg != null)
            {
                msg.Status = status;
            }
        }

        private void UsersList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (UsersList.SelectedItem is UserViewModel user)
            {
                _currentChatUser = user.Username;
                ChatUsername.Text = user.Username;
                ChatStatus.Text = "online";
                _messages.Clear();
                MessageBox.Focus();
            }
        }

        private async void Send_Click(object sender, RoutedEventArgs e)
        {
            await SendMessage();
        }

        private async void MessageBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter && Keyboard.Modifiers != ModifierKeys.Shift)
            {
                e.Handled = true;
                await SendMessage();
            }
        }

        private async void MessageBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (!string.IsNullOrEmpty(_currentChatUser) && !_isTyping && MessageBox.Text.Length > 0)
            {
                _isTyping = true;
                await SendTypingIndicator();
                _typingTimer.Start();
            }
        }

        private async Task SendTypingIndicator()
        {
            if (_webSocket?.State == WebSocketState.Open && !string.IsNullOrEmpty(_currentChatUser))
            {
                var typingMsg = new Message
                {
                    Type = MessageType.Typing,
                    From = _username,
                    To = _currentChatUser,
                    Content = "typing..."
                };

                var json = JsonConvert.SerializeObject(typingMsg);
                var bytes = Encoding.UTF8.GetBytes(json);
                await _webSocket.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, CancellationToken.None);
            }
        }

        private async Task SendMessage()
        {
            var text = MessageBox.Text.Trim();
            
            if (string.IsNullOrEmpty(text) || string.IsNullOrEmpty(_currentChatUser))
                return;

            if (_webSocket?.State != WebSocketState.Open)
            {
                MessageBox.Show("Not connected to server!", "HARDCORE", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var message = new Message
            {
                Type = MessageType.Text,
                From = _username,
                To = _currentChatUser,
                Content = text,
                Timestamp = DateTime.Now
            };

            AddMessageToChat(message);

            var json = JsonConvert.SerializeObject(message);
            var bytes = Encoding.UTF8.GetBytes(json);
            await _webSocket.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, CancellationToken.None);

            MessageBox.Clear();
            ChatStatus.Text = "online";
        }
    }

    public class UserViewModel : System.ComponentModel.INotifyPropertyChanged
    {
        private string _statusText;

        public string Username { get; set; }
        public string Avatar { get; set; }
        
        public string StatusText
        {
            get => _statusText;
            set
            {
                _statusText = value;
                OnPropertyChanged(nameof(StatusText));
            }
        }

        public Visibility OnlineVisibility { get; set; }

        public event System.ComponentModel.PropertyChangedEventHandler PropertyChanged;
        
        protected void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(propertyName));
        }
    }

    public class MessageViewModel : System.ComponentModel.INotifyPropertyChanged
    {
        private string _status = "";

        public string Content { get; set; }
        public string TimeString { get; set; }
        public HorizontalAlignment Alignment { get; set; }
        public Brush BubbleColor { get; set; }
        public string MessageId { get; set; }

        public string Status
        {
            get => _status;
            set
            {
                _status = value;
                OnPropertyChanged(nameof(Status));
            }
        }

        public event System.ComponentModel.PropertyChangedEventHandler PropertyChanged;
        
        protected void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(propertyName));
        }
    }
}
