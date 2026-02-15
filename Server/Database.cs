using System;
using System.Collections.Generic;
using System.Data;
using Microsoft.Data.Sqlite;
using HardcoreMessenger.Shared;
using Newtonsoft.Json;

namespace HardcoreServer
{
    public class Database
    {
        private readonly string _connectionString;

        public Database(string dbPath = "hardcore_messenger.db")
        {
            _connectionString = $"Data Source={dbPath}";
            InitializeDatabase();
        }

        private void InitializeDatabase()
        {
            using var connection = new SqliteConnection(_connectionString);
            connection.Open();

            var command = connection.CreateCommand();
            command.CommandText = @"
                -- Таблица пользователей
                CREATE TABLE IF NOT EXISTS Users (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    Username TEXT UNIQUE NOT NULL,
                    PasswordHash TEXT NOT NULL,
                    Email TEXT,
                    Avatar TEXT,
                    AvatarType TEXT,
                    Bio TEXT,
                    Phone TEXT,
                    CustomStatus TEXT,
                    IsPremium INTEGER DEFAULT 0,
                    CreatedAt TEXT NOT NULL,
                    LastSeen TEXT
                );

                -- Таблица сообщений
                CREATE TABLE IF NOT EXISTS Messages (
                    Id TEXT PRIMARY KEY,
                    FromUsername TEXT NOT NULL,
                    ToUsername TEXT NOT NULL,
                    Content TEXT NOT NULL,
                    MessageType TEXT NOT NULL,
                    Timestamp TEXT NOT NULL,
                    IsRead INTEGER DEFAULT 0,
                    IsEdited INTEGER DEFAULT 0,
                    ReplyToId TEXT,
                    Attachments TEXT,
                    FOREIGN KEY (FromUsername) REFERENCES Users(Username),
                    FOREIGN KEY (ToUsername) REFERENCES Users(Username)
                );

                -- Таблица сессий (для отслеживания онлайн)
                CREATE TABLE IF NOT EXISTS Sessions (
                    Id TEXT PRIMARY KEY,
                    Username TEXT NOT NULL,
                    IPAddress TEXT,
                    ConnectedAt TEXT NOT NULL,
                    LastActivity TEXT NOT NULL,
                    FOREIGN KEY (Username) REFERENCES Users(Username)
                );

                -- Индексы для быстрого поиска
                CREATE INDEX IF NOT EXISTS idx_messages_from ON Messages(FromUsername);
                CREATE INDEX IF NOT EXISTS idx_messages_to ON Messages(ToUsername);
                CREATE INDEX IF NOT EXISTS idx_messages_timestamp ON Messages(Timestamp);
            ";
            command.ExecuteNonQuery();

            Console.WriteLine("[DB] ✓ Database initialized");
        }

        // ============================================
        // РЕГИСТРАЦИЯ И АВТОРИЗАЦИЯ
        // ============================================

        public bool RegisterUser(string username, string password, string email = null)
        {
            try
            {
                using var connection = new SqliteConnection(_connectionString);
                connection.Open();

                // Хешируем пароль
                string passwordHash = BCrypt.Net.BCrypt.HashPassword(password);

                var command = connection.CreateCommand();
                command.CommandText = @"
                    INSERT INTO Users (Username, PasswordHash, Email, CreatedAt, Avatar, AvatarType)
                    VALUES (@username, @passwordHash, @email, @createdAt, @avatar, @avatarType)
                ";
                command.Parameters.AddWithValue("@username", username);
                command.Parameters.AddWithValue("@passwordHash", passwordHash);
                command.Parameters.AddWithValue("@email", email ?? (object)DBNull.Value);
                command.Parameters.AddWithValue("@createdAt", DateTime.UtcNow.ToString("o"));
                command.Parameters.AddWithValue("@avatar", username.Substring(0, 1).ToUpper());
                command.Parameters.AddWithValue("@avatarType", "emoji");

                command.ExecuteNonQuery();

                Console.WriteLine($"[DB] ✓ User registered: {username}");
                return true;
            }
            catch (SqliteException ex) when (ex.SqliteErrorCode == 19) // UNIQUE constraint
            {
                Console.WriteLine($"[DB] ✗ Username already exists: {username}");
                return false;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[DB ERROR] Registration failed: {ex.Message}");
                return false;
            }
        }

        public bool LoginUser(string username, string password)
        {
            try
            {
                using var connection = new SqliteConnection(_connectionString);
                connection.Open();

                var command = connection.CreateCommand();
                command.CommandText = "SELECT PasswordHash FROM Users WHERE Username = @username";
                command.Parameters.AddWithValue("@username", username);

                var result = command.ExecuteScalar();
                if (result == null)
                {
                    Console.WriteLine($"[DB] ✗ User not found: {username}");
                    return false;
                }

                string storedHash = result.ToString();
                bool isValid = BCrypt.Net.BCrypt.Verify(password, storedHash);

                if (isValid)
                {
                    // Обновляем LastSeen
                    var updateCmd = connection.CreateCommand();
                    updateCmd.CommandText = "UPDATE Users SET LastSeen = @lastSeen WHERE Username = @username";
                    updateCmd.Parameters.AddWithValue("@lastSeen", DateTime.UtcNow.ToString("o"));
                    updateCmd.Parameters.AddWithValue("@username", username);
                    updateCmd.ExecuteNonQuery();

                    Console.WriteLine($"[DB] ✓ User logged in: {username}");
                }
                else
                {
                    Console.WriteLine($"[DB] ✗ Invalid password for: {username}");
                }

                return isValid;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[DB ERROR] Login failed: {ex.Message}");
                return false;
            }
        }

        public bool UserExists(string username)
        {
            using var connection = new SqliteConnection(_connectionString);
            connection.Open();

            var command = connection.CreateCommand();
            command.CommandText = "SELECT COUNT(*) FROM Users WHERE Username = @username";
            command.Parameters.AddWithValue("@username", username);

            long count = (long)command.ExecuteScalar();
            return count > 0;
        }

        // ============================================
        // ПРОФИЛИ ПОЛЬЗОВАТЕЛЕЙ
        // ============================================

        public ProfileData GetUserProfile(string username)
        {
            using var connection = new SqliteConnection(_connectionString);
            connection.Open();

            var command = connection.CreateCommand();
            command.CommandText = @"
                SELECT Username, Avatar, AvatarType, Bio, Phone, CustomStatus, IsPremium
                FROM Users WHERE Username = @username
            ";
            command.Parameters.AddWithValue("@username", username);

            using var reader = command.ExecuteReader();
            if (reader.Read())
            {
                return new ProfileData
                {
                    Username = reader.GetString(0),
                    Avatar = reader.IsDBNull(1) ? null : reader.GetString(1),
                    AvatarType = reader.IsDBNull(2) ? "emoji" : reader.GetString(2),
                    Bio = reader.IsDBNull(3) ? null : reader.GetString(3),
                    Phone = reader.IsDBNull(4) ? null : reader.GetString(4),
                    CustomStatus = reader.IsDBNull(5) ? null : reader.GetString(5),
                    IsPremium = reader.GetInt32(6) == 1
                };
            }

            return null;
        }

        public void UpdateUserProfile(ProfileData profile)
        {
            using var connection = new SqliteConnection(_connectionString);
            connection.Open();

            var command = connection.CreateCommand();
            command.CommandText = @"
                UPDATE Users SET
                    Avatar = @avatar,
                    AvatarType = @avatarType,
                    Bio = @bio,
                    Phone = @phone,
                    CustomStatus = @customStatus
                WHERE Username = @username
            ";
            command.Parameters.AddWithValue("@username", profile.Username);
            command.Parameters.AddWithValue("@avatar", profile.Avatar ?? (object)DBNull.Value);
            command.Parameters.AddWithValue("@avatarType", profile.AvatarType ?? "emoji");
            command.Parameters.AddWithValue("@bio", profile.Bio ?? (object)DBNull.Value);
            command.Parameters.AddWithValue("@phone", profile.Phone ?? (object)DBNull.Value);
            command.Parameters.AddWithValue("@customStatus", profile.CustomStatus ?? (object)DBNull.Value);

            command.ExecuteNonQuery();
            Console.WriteLine($"[DB] ✓ Profile updated: {profile.Username}");
        }

        public List<string> GetAllUsers()
        {
            var users = new List<string>();

            using var connection = new SqliteConnection(_connectionString);
            connection.Open();

            var command = connection.CreateCommand();
            command.CommandText = "SELECT Username FROM Users ORDER BY Username";

            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                users.Add(reader.GetString(0));
            }

            return users;
        }

        // ============================================
        // СООБЩЕНИЯ
        // ============================================

        public void SaveMessage(Message message)
        {
            using var connection = new SqliteConnection(_connectionString);
            connection.Open();

            var command = connection.CreateCommand();
            command.CommandText = @"
                INSERT INTO Messages (Id, FromUsername, ToUsername, Content, MessageType, Timestamp, IsRead, IsEdited, ReplyToId, Attachments)
                VALUES (@id, @from, @to, @content, @type, @timestamp, @isRead, @isEdited, @replyToId, @attachments)
            ";
            command.Parameters.AddWithValue("@id", message.Id);
            command.Parameters.AddWithValue("@from", message.From);
            command.Parameters.AddWithValue("@to", message.To);
            command.Parameters.AddWithValue("@content", message.Content ?? "");
            command.Parameters.AddWithValue("@type", message.Type.ToString());
            command.Parameters.AddWithValue("@timestamp", message.Timestamp.ToString("o"));
            command.Parameters.AddWithValue("@isRead", message.IsRead ? 1 : 0);
            command.Parameters.AddWithValue("@isEdited", message.IsEdited ? 1 : 0);
            command.Parameters.AddWithValue("@replyToId", message.ReplyToId ?? (object)DBNull.Value);
            command.Parameters.AddWithValue("@attachments", message.Attachments != null && message.Attachments.Count > 0 
                ? JsonConvert.SerializeObject(message.Attachments) 
                : (object)DBNull.Value);

            command.ExecuteNonQuery();
        }

        public List<Message> GetMessageHistory(string user1, string user2, int limit = 100)
        {
            var messages = new List<Message>();

            using var connection = new SqliteConnection(_connectionString);
            connection.Open();

            var command = connection.CreateCommand();
            command.CommandText = @"
                SELECT Id, FromUsername, ToUsername, Content, MessageType, Timestamp, IsRead, IsEdited, ReplyToId, Attachments
                FROM Messages
                WHERE (FromUsername = @user1 AND ToUsername = @user2)
                   OR (FromUsername = @user2 AND ToUsername = @user1)
                ORDER BY Timestamp DESC
                LIMIT @limit
            ";
            command.Parameters.AddWithValue("@user1", user1);
            command.Parameters.AddWithValue("@user2", user2);
            command.Parameters.AddWithValue("@limit", limit);

            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                var message = new Message
                {
                    Id = reader.GetString(0),
                    From = reader.GetString(1),
                    To = reader.GetString(2),
                    Content = reader.GetString(3),
                    Type = Enum.Parse<MessageType>(reader.GetString(4)),
                    Timestamp = DateTime.Parse(reader.GetString(5)),
                    IsRead = reader.GetInt32(6) == 1,
                    IsEdited = reader.GetInt32(7) == 1,
                    ReplyToId = reader.IsDBNull(8) ? null : reader.GetString(8)
                };

                if (!reader.IsDBNull(9))
                {
                    message.Attachments = JsonConvert.DeserializeObject<List<string>>(reader.GetString(9));
                }

                messages.Add(message);
            }

            messages.Reverse(); // Сортируем от старых к новым
            return messages;
        }

        public void MarkMessagesAsRead(string from, string to)
        {
            using var connection = new SqliteConnection(_connectionString);
            connection.Open();

            var command = connection.CreateCommand();
            command.CommandText = @"
                UPDATE Messages 
                SET IsRead = 1 
                WHERE FromUsername = @from AND ToUsername = @to AND IsRead = 0
            ";
            command.Parameters.AddWithValue("@from", from);
            command.Parameters.AddWithValue("@to", to);

            int updated = command.ExecuteNonQuery();
            if (updated > 0)
            {
                Console.WriteLine($"[DB] ✓ Marked {updated} messages as read");
            }
        }

        // ============================================
        // СЕССИИ
        // ============================================

        public void CreateSession(string sessionId, string username, string ipAddress)
        {
            using var connection = new SqliteConnection(_connectionString);
            connection.Open();

            var command = connection.CreateCommand();
            command.CommandText = @"
                INSERT INTO Sessions (Id, Username, IPAddress, ConnectedAt, LastActivity)
                VALUES (@id, @username, @ip, @connected, @lastActivity)
            ";
            command.Parameters.AddWithValue("@id", sessionId);
            command.Parameters.AddWithValue("@username", username);
            command.Parameters.AddWithValue("@ip", ipAddress ?? "");
            command.Parameters.AddWithValue("@connected", DateTime.UtcNow.ToString("o"));
            command.Parameters.AddWithValue("@lastActivity", DateTime.UtcNow.ToString("o"));

            command.ExecuteNonQuery();
        }

        public void RemoveSession(string sessionId)
        {
            using var connection = new SqliteConnection(_connectionString);
            connection.Open();

            var command = connection.CreateCommand();
            command.CommandText = "DELETE FROM Sessions WHERE Id = @id";
            command.Parameters.AddWithValue("@id", sessionId);

            command.ExecuteNonQuery();
        }

        public void UpdateSessionActivity(string sessionId)
        {
            using var connection = new SqliteConnection(_connectionString);
            connection.Open();

            var command = connection.CreateCommand();
            command.CommandText = "UPDATE Sessions SET LastActivity = @lastActivity WHERE Id = @id";
            command.Parameters.AddWithValue("@lastActivity", DateTime.UtcNow.ToString("o"));
            command.Parameters.AddWithValue("@id", sessionId);

            command.ExecuteNonQuery();
        }
    }
}
