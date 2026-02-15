using System;
using System.Collections.Generic;
using Npgsql;
using HardcoreMessenger.Shared;
using Newtonsoft.Json;

namespace HardcoreServer
{
    public class DatabasePostgres
    {
        private readonly string _connectionString;

        public DatabasePostgres(string connectionString)
        {
            _connectionString = connectionString;
            InitializeDatabase();
        }

        private void InitializeDatabase()
        {
            using var connection = new NpgsqlConnection(_connectionString);
            connection.Open();

            using var command = connection.CreateCommand();
            command.CommandText = @"
                -- Таблица пользователей
                CREATE TABLE IF NOT EXISTS users (
                    id SERIAL PRIMARY KEY,
                    username VARCHAR(50) UNIQUE NOT NULL,
                    password_hash TEXT NOT NULL,
                    email VARCHAR(100),
                    avatar TEXT,
                    avatar_type VARCHAR(20) DEFAULT 'emoji',
                    bio TEXT,
                    phone VARCHAR(20),
                    custom_status TEXT,
                    is_premium BOOLEAN DEFAULT FALSE,
                    created_at TIMESTAMP NOT NULL DEFAULT NOW(),
                    last_seen TIMESTAMP
                );

                -- Таблица сообщений
                CREATE TABLE IF NOT EXISTS messages (
                    id VARCHAR(50) PRIMARY KEY,
                    from_username VARCHAR(50) NOT NULL,
                    to_username VARCHAR(50) NOT NULL,
                    content TEXT NOT NULL,
                    message_type VARCHAR(30) NOT NULL,
                    timestamp TIMESTAMP NOT NULL,
                    is_read BOOLEAN DEFAULT FALSE,
                    is_edited BOOLEAN DEFAULT FALSE,
                    reply_to_id VARCHAR(50),
                    attachments JSONB,
                    FOREIGN KEY (from_username) REFERENCES users(username),
                    FOREIGN KEY (to_username) REFERENCES users(username)
                );

                -- Таблица сессий
                CREATE TABLE IF NOT EXISTS sessions (
                    id VARCHAR(50) PRIMARY KEY,
                    username VARCHAR(50) NOT NULL,
                    ip_address VARCHAR(50),
                    connected_at TIMESTAMP NOT NULL DEFAULT NOW(),
                    last_activity TIMESTAMP NOT NULL DEFAULT NOW(),
                    FOREIGN KEY (username) REFERENCES users(username)
                );

                -- Индексы для быстрого поиска
                CREATE INDEX IF NOT EXISTS idx_messages_from ON messages(from_username);
                CREATE INDEX IF NOT EXISTS idx_messages_to ON messages(to_username);
                CREATE INDEX IF NOT EXISTS idx_messages_timestamp ON messages(timestamp);
                CREATE INDEX IF NOT EXISTS idx_messages_chat ON messages(from_username, to_username, timestamp);
            ";
            command.ExecuteNonQuery();

            Console.WriteLine("[DB] ✓ PostgreSQL database initialized");
        }

        // ============================================
        // РЕГИСТРАЦИЯ И АВТОРИЗАЦИЯ
        // ============================================

        public bool RegisterUser(string username, string password, string email = null)
        {
            try
            {
                using var connection = new NpgsqlConnection(_connectionString);
                connection.Open();

                string passwordHash = BCrypt.Net.BCrypt.HashPassword(password);

                using var command = connection.CreateCommand();
                command.CommandText = @"
                    INSERT INTO users (username, password_hash, email, created_at, avatar, avatar_type)
                    VALUES (@username, @passwordHash, @email, @createdAt, @avatar, @avatarType)
                ";
                command.Parameters.AddWithValue("username", username);
                command.Parameters.AddWithValue("passwordHash", passwordHash);
                command.Parameters.AddWithValue("email", email ?? (object)DBNull.Value);
                command.Parameters.AddWithValue("createdAt", DateTime.UtcNow);
                command.Parameters.AddWithValue("avatar", username.Substring(0, 1).ToUpper());
                command.Parameters.AddWithValue("avatarType", "emoji");

                command.ExecuteNonQuery();

                Console.WriteLine($"[DB] ✓ User registered: {username}");
                return true;
            }
            catch (PostgresException ex) when (ex.SqlState == "23505") // UNIQUE violation
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
                using var connection = new NpgsqlConnection(_connectionString);
                connection.Open();

                using var command = connection.CreateCommand();
                command.CommandText = "SELECT password_hash FROM users WHERE username = @username";
                command.Parameters.AddWithValue("username", username);

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
                    using var updateCmd = connection.CreateCommand();
                    updateCmd.CommandText = "UPDATE users SET last_seen = @lastSeen WHERE username = @username";
                    updateCmd.Parameters.AddWithValue("lastSeen", DateTime.UtcNow);
                    updateCmd.Parameters.AddWithValue("username", username);
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
            using var connection = new NpgsqlConnection(_connectionString);
            connection.Open();

            using var command = connection.CreateCommand();
            command.CommandText = "SELECT COUNT(*) FROM users WHERE username = @username";
            command.Parameters.AddWithValue("username", username);

            long count = (long)command.ExecuteScalar();
            return count > 0;
        }

        // ============================================
        // ПРОФИЛИ ПОЛЬЗОВАТЕЛЕЙ
        // ============================================

        public ProfileData GetUserProfile(string username)
        {
            using var connection = new NpgsqlConnection(_connectionString);
            connection.Open();

            using var command = connection.CreateCommand();
            command.CommandText = @"
                SELECT username, avatar, avatar_type, bio, phone, custom_status, is_premium
                FROM users WHERE username = @username
            ";
            command.Parameters.AddWithValue("username", username);

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
                    IsPremium = reader.GetBoolean(6)
                };
            }

            return null;
        }

        public void UpdateUserProfile(ProfileData profile)
        {
            using var connection = new NpgsqlConnection(_connectionString);
            connection.Open();

            using var command = connection.CreateCommand();
            command.CommandText = @"
                UPDATE users SET
                    avatar = @avatar,
                    avatar_type = @avatarType,
                    bio = @bio,
                    phone = @phone,
                    custom_status = @customStatus
                WHERE username = @username
            ";
            command.Parameters.AddWithValue("username", profile.Username);
            command.Parameters.AddWithValue("avatar", profile.Avatar ?? (object)DBNull.Value);
            command.Parameters.AddWithValue("avatarType", profile.AvatarType ?? "emoji");
            command.Parameters.AddWithValue("bio", profile.Bio ?? (object)DBNull.Value);
            command.Parameters.AddWithValue("phone", profile.Phone ?? (object)DBNull.Value);
            command.Parameters.AddWithValue("customStatus", profile.CustomStatus ?? (object)DBNull.Value);

            command.ExecuteNonQuery();
            Console.WriteLine($"[DB] ✓ Profile updated: {profile.Username}");
        }

        public List<string> GetAllUsers()
        {
            var users = new List<string>();

            using var connection = new NpgsqlConnection(_connectionString);
            connection.Open();

            using var command = connection.CreateCommand();
            command.CommandText = "SELECT username FROM users ORDER BY username";

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
            using var connection = new NpgsqlConnection(_connectionString);
            connection.Open();

            using var command = connection.CreateCommand();
            command.CommandText = @"
                INSERT INTO messages (id, from_username, to_username, content, message_type, timestamp, is_read, is_edited, reply_to_id, attachments)
                VALUES (@id, @from, @to, @content, @type, @timestamp, @isRead, @isEdited, @replyToId, @attachments::jsonb)
            ";
            command.Parameters.AddWithValue("id", message.Id);
            command.Parameters.AddWithValue("from", message.From);
            command.Parameters.AddWithValue("to", message.To);
            command.Parameters.AddWithValue("content", message.Content ?? "");
            command.Parameters.AddWithValue("type", message.Type.ToString());
            command.Parameters.AddWithValue("timestamp", message.Timestamp);
            command.Parameters.AddWithValue("isRead", message.IsRead);
            command.Parameters.AddWithValue("isEdited", message.IsEdited);
            command.Parameters.AddWithValue("replyToId", message.ReplyToId ?? (object)DBNull.Value);
            command.Parameters.AddWithValue("attachments", 
                message.Attachments != null && message.Attachments.Count > 0 
                    ? JsonConvert.SerializeObject(message.Attachments) 
                    : (object)DBNull.Value);

            command.ExecuteNonQuery();
        }

        public List<Message> GetMessageHistory(string user1, string user2, int limit = 100)
        {
            var messages = new List<Message>();

            using var connection = new NpgsqlConnection(_connectionString);
            connection.Open();

            using var command = connection.CreateCommand();
            command.CommandText = @"
                SELECT id, from_username, to_username, content, message_type, timestamp, is_read, is_edited, reply_to_id, attachments
                FROM messages
                WHERE (from_username = @user1 AND to_username = @user2)
                   OR (from_username = @user2 AND to_username = @user1)
                ORDER BY timestamp ASC
                LIMIT @limit
            ";
            command.Parameters.AddWithValue("user1", user1);
            command.Parameters.AddWithValue("user2", user2);
            command.Parameters.AddWithValue("limit", limit);

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
                    Timestamp = reader.GetDateTime(5),
                    IsRead = reader.GetBoolean(6),
                    IsEdited = reader.GetBoolean(7),
                    ReplyToId = reader.IsDBNull(8) ? null : reader.GetString(8)
                };

                if (!reader.IsDBNull(9))
                {
                    var json = reader.GetString(9);
                    message.Attachments = JsonConvert.DeserializeObject<List<string>>(json);
                }

                messages.Add(message);
            }

            return messages;
        }

        public void MarkMessagesAsRead(string from, string to)
        {
            using var connection = new NpgsqlConnection(_connectionString);
            connection.Open();

            using var command = connection.CreateCommand();
            command.CommandText = @"
                UPDATE messages 
                SET is_read = TRUE 
                WHERE from_username = @from AND to_username = @to AND is_read = FALSE
            ";
            command.Parameters.AddWithValue("from", from);
            command.Parameters.AddWithValue("to", to);

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
            using var connection = new NpgsqlConnection(_connectionString);
            connection.Open();

            using var command = connection.CreateCommand();
            command.CommandText = @"
                INSERT INTO sessions (id, username, ip_address, connected_at, last_activity)
                VALUES (@id, @username, @ip, @connected, @lastActivity)
            ";
            command.Parameters.AddWithValue("id", sessionId);
            command.Parameters.AddWithValue("username", username);
            command.Parameters.AddWithValue("ip", ipAddress ?? "unknown");
            command.Parameters.AddWithValue("connected", DateTime.UtcNow);
            command.Parameters.AddWithValue("lastActivity", DateTime.UtcNow);

            command.ExecuteNonQuery();
        }

        public void RemoveSession(string sessionId)
        {
            using var connection = new NpgsqlConnection(_connectionString);
            connection.Open();

            using var command = connection.CreateCommand();
            command.CommandText = "DELETE FROM sessions WHERE id = @id";
            command.Parameters.AddWithValue("id", sessionId);

            command.ExecuteNonQuery();
        }

        public void UpdateSessionActivity(string sessionId)
        {
            using var connection = new NpgsqlConnection(_connectionString);
            connection.Open();

            using var command = connection.CreateCommand();
            command.CommandText = "UPDATE sessions SET last_activity = @lastActivity WHERE id = @id";
            command.Parameters.AddWithValue("lastActivity", DateTime.UtcNow);
            command.Parameters.AddWithValue("id", sessionId);

            command.ExecuteNonQuery();
        }
    }
}
