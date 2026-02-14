using System;
using System.Collections.Generic;

namespace HardcoreMessenger.Shared
{
    public class Message
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string From { get; set; }
        public string To { get; set; }
        public string Content { get; set; }
        public DateTime Timestamp { get; set; } = DateTime.Now;
        public MessageType Type { get; set; }
        public bool IsRead { get; set; }
        public string ReplyToId { get; set; } // Для ответов на сообщения
        public bool IsEdited { get; set; } // Было ли отредактировано
        public List<string> Attachments { get; set; } = new List<string>(); // Файлы
        public bool IsPinned { get; set; } // Закреплено ли
        public string ForwardedFrom { get; set; } // От кого переслано
        public string GroupId { get; set; } // ID группы (если групповое)
        public List<string> Mentions { get; set; } = new List<string>(); // Упоминания @user
        public int VoiceDuration { get; set; } // Длительность голосового (секунды)
        public Dictionary<string, string> Reactions { get; set; } = new Dictionary<string, string>(); // Username -> Emoji
        public PollData Poll { get; set; } // Данные опроса
        public LocationData Location { get; set; } // Геолокация
    }

    public enum MessageType
    {
        Text,
        Login,
        Logout,
        UserList,
        Typing,
        Delivered,
        Read,
        Image,
        File,
        ProfileUpdate, // Обновление профиля
        AvatarUpdate, // Обновление аватарки
        StatusUpdate, // Обновление статуса
        VoiceCall, // Голосовой звонок
        VideoCall, // Видео звонок
        Reaction, // Реакция на сообщение
        Voice, // Голосовое сообщение
        Forward, // Пересылка
        GroupCreate, // Создание группы
        GroupMessage, // Сообщение в группу
        Pinned, // Закрепленное сообщение
        Deleted, // Удаленное сообщение
        Animation, // GIF анимация
        Sticker, // Стикер
        Poll, // Опрос
        Location, // Геолокация
        Contact, // Контакт
        Music, // Музыкальный файл
        Video, // Видео
        ScreenShare, // Демонстрация экрана
        GameInvite, // Приглашение в игру
        Payment, // Платёж/перевод
        Reminder, // Напоминание
        Schedule // Запланированное сообщение
    }

    public class User
    {
        public string Username { get; set; }
        public string Avatar { get; set; } // Base64 изображение или emoji
        public string AvatarType { get; set; } // "emoji" или "image"
        public UserStatus Status { get; set; }
        public DateTime LastSeen { get; set; }
        public string CustomStatus { get; set; } // Кастомный статус пользователя
        public string Bio { get; set; } // О себе
        public string Phone { get; set; } // Телефон
        public bool IsPremium { get; set; } // HARD+ премиум статус
        public string PremiumEmoji { get; set; } // Эмодзи после ника (только для премиум)
        public string PremiumBadge { get; set; } // Значок премиум статуса
        public DateTime? PremiumExpiresAt { get; set; } // Когда истекает премиум
        public List<string> PremiumStickers { get; set; } = new List<string>(); // Эксклюзивные стикеры
        public string Theme { get; set; } // Кастомная тема (для премиум)
        public List<string> Achievements { get; set; } = new List<string>(); // Достижения
        public int MessageCount { get; set; } // Счётчик сообщений
        public int Level { get; set; } // Уровень пользователя
        public bool IsStreaming { get; set; } // Идёт ли стрим/демонстрация экрана
        public string CurrentGame { get; set; } // Во что сейчас играет
        public string Mood { get; set; } // Настроение (эмодзи)
        public DateTime RegisteredAt { get; set; } // Дата регистрации
    }

    public enum UserStatus
    {
        Online,
        Offline,
        Away,
        Busy,
        DoNotDisturb
    }

    public class ChatRoom
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public List<string> Members { get; set; } = new List<string>();
        public DateTime Created { get; set; }
        public string Avatar { get; set; }
    }

    public class ProfileData
    {
        public string Username { get; set; }
        public string Avatar { get; set; }
        public string AvatarType { get; set; }
        public string Bio { get; set; }
        public string Phone { get; set; }
        public string CustomStatus { get; set; }
        public bool IsPremium { get; set; }
        public string PremiumEmoji { get; set; }
        public string PremiumBadge { get; set; }
        public string Theme { get; set; }
    }

    // Данные опроса
    public class PollData
    {
        public string Question { get; set; }
        public List<PollOption> Options { get; set; } = new List<PollOption>();
        public bool IsAnonymous { get; set; }
        public bool MultipleChoice { get; set; }
        public DateTime? ClosesAt { get; set; }
    }

    public class PollOption
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string Text { get; set; }
        public List<string> Voters { get; set; } = new List<string>(); // Usernames
        public int VoteCount => Voters.Count;
    }

    // Данные геолокации
    public class LocationData
    {
        public double Latitude { get; set; }
        public double Longitude { get; set; }
        public string Address { get; set; }
        public string Title { get; set; }
    }
}
