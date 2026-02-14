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
        private ClientWebSocket _webSocket;
        private string _username;
        private string _currentChatUser;
        private ObservableCollection<UserViewModel> _users = new ObservableCollection<UserViewModel>();
        private ObservableCollection<MessageViewModel> _messages = new ObservableCollection<MessageViewModel>();
        private CancellationTokenSource _cancellationTokenSource;
        private bool _isTyping = false;
        private System.Windows.Threading.DispatcherTimer _typingTimer;

        public MainWindow()
        {
            InitializeComponent();
            UsersList.ItemsSource = _users;
            MessagesPanel.ItemsSource = _messages;

            _typingTimer = new System.Windows.Threading.DispatcherTimer();
            _typingTimer.Interval = TimeSpan.FromSeconds(2);
            _typingTimer.Tick += (s, e) =>
            {
                _isTyping = false;
                _typingTimer.Stop();
            };
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

        // РАБОТАЮЩЕЕ МЕНЮ
        private void ToggleMenu_Click(object sender, RoutedEventArgs e)
        {
            if (SideMenu.Visibility == Visibility.Collapsed)
            {
                SideMenu.Visibility = Visibility.Visible;
                var anim = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(200));
                SideMenu.BeginAnimation(OpacityProperty, anim);
            }
            else
            {
                var anim = new DoubleAnimation(1, 0, TimeSpan.FromMilliseconds(200));
                anim.Completed += (s, args) => SideMenu.Visibility = Visibility.Collapsed;
                SideMenu.BeginAnimation(OpacityProperty, anim);
            }
        }

        // ОТКРЫТИЕ ПРОФИЛЯ
        private void Profile_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show($"Profile: {_username}\nStatus: Online\n\nThis is a demo messenger!", 
                "My Profile", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        // НАСТРОЙКИ
        private void Settings_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("Settings:\n\n• Notifications: ON\n• Theme: Dark\n• Language: English\n\nThis is a demo!", 
                "Settings", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        // ЗВОНОК
        private void Call_Click(object sender, RoutedEventArgs e)
        {
            if (!string.IsNullOrEmpty(_currentChatUser))
            {
                MessageBox.Show($"Calling {_currentChatUser}...\n\nVoice calls coming soon!", 
                    "Call", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        // МЕНЮ ЧАТА
        private void ChatMenu_Click(object sender, RoutedEventArgs e)
        {
            if (InfoPanel.Visibility == Visibility.Visible)
            {
                InfoPanel.Visibility = Visibility.Collapsed;
            }
            else
            {
                InfoPanel.Visibility = Visibility.Visible;
            }
        }

        private async void Login_Click(object sender, RoutedEventArgs e)
        {
            await ConnectToServer();
        }

        private async void UsernameBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
                await ConnectToServer();
        }

        private async Task ConnectToServer()
        {
            _username = UsernameBox.Text.Trim();
            if (string.IsNullOrEmpty(_username))
            {
                MessageBox.Show("Please enter a username!", "Telegram", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                StatusText.Text = "Connecting...";
                StatusText.Foreground = new SolidColorBrush(Color.FromRgb(255, 193, 7));
                
                _webSocket = new ClientWebSocket();
                _cancellationTokenSource = new CancellationTokenSource();

                var uri = new Uri($"ws://localhost:8080/?username={_username}");
                await _webSocket.ConnectAsync(uri, _cancellationTokenSource.Token);

                StatusText.Text = $"● {_username}";
                StatusText.Foreground = new SolidColorBrush(Color.FromRgb(52, 199, 89));

                // Обновляем все места где показывается имя
                if (NavAvatarText != null)
                    NavAvatarText.Text = _username.Substring(0, 1).ToUpper();
                
                if (MenuAvatarText != null)
                    MenuAvatarText.Text = _username.Substring(0, 1).ToUpper();
                
                if (MenuUsernameText != null)
                    MenuUsernameText.Text = _username;

                // Анимация переключения
                var fadeOut = new DoubleAnimation(1, 0, TimeSpan.FromMilliseconds(300));
                fadeOut.Completed += (s, e) =>
                {
                    LoginPanel.Visibility = Visibility.Collapsed;
                    ChatPanel.Visibility = Visibility.Visible;
                    
                    var fadeIn = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(400));
                    ChatPanel.BeginAnimation(OpacityProperty, fadeIn);
                };
                LoginPanel.BeginAnimation(OpacityProperty, fadeOut);

                _ = ReceiveMessages();
            }
            catch (Exception ex)
            {
                StatusText.Text = "Connection failed";
                StatusText.Foreground = new SolidColorBrush(Color.FromRgb(255, 59, 48));
                
                MessageBox.Show($"Failed to connect to server!\n\nMake sure the server is running.\n\nError: {ex.Message}", 
                    "Connection Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

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
                        StatusText.Foreground = new SolidColorBrush(Color.FromRgb(255, 59, 48));
                        MessageBox.Show($"Connection lost: {ex.Message}", "Telegram", MessageBoxButton.OK, MessageBoxImage.Error);
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
                    new SolidColorBrush(Color.FromRgb(139, 92, 246)) : 
                    new SolidColorBrush(Color.FromRgb(44, 44, 46)),
                MessageId = message.Id
            };

            _messages.Add(msgViewModel);
            MessagesScroll.ScrollToEnd();
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
                
                // Обновляем правую панель
                if (InfoAvatar != null)
                    InfoAvatar.Text = user.Avatar;
                if (InfoUsername != null)
                    InfoUsername.Text = user.Username;
                if (InfoUsernameDetail != null)
                    InfoUsernameDetail.Text = $"@{user.Username.ToLower()}";
                
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
                MessageBox.Show("Not connected to server!", "Telegram", MessageBoxButton.OK, MessageBoxImage.Warning);
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
