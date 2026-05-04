using HermesAI.MVVM.Model;
using Microsoft.Data.Sqlite;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;

namespace HermesAI.MVVM.Services
{
    public class LocalStorageRepository : IChatRepository
    {
        private readonly string _dbPath;

        public LocalStorageRepository()
        {
            string appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            string folderPath = Path.Combine(appDataPath, "HermesAI");

            Directory.CreateDirectory(folderPath);

            _dbPath = $"Data Source={Path.Combine(folderPath, "hermesai.db")};";

            InitializeDatabase();
        }

        private void InitializeDatabase()
        {
            using (var connection = new SqliteConnection(_dbPath))
            {
                connection.Open();
                var command = connection.CreateCommand();

                command.CommandText = @"
                    CREATE TABLE IF NOT EXISTS Chats (
                        Id TEXT PRIMARY KEY,
                        Title TEXT
                    );

                    CREATE TABLE IF NOT EXISTS Messages (
                        Id INTEGER PRIMARY KEY AUTOINCREMENT,
                        ChatId TEXT,
                        Text TEXT,
                        IsMyMessage INTEGER,
                        Timestamp TEXT,
                        FOREIGN KEY(ChatId) REFERENCES Chats(Id) ON DELETE CASCADE
                    );
                ";
                command.ExecuteNonQuery();
            }
        }

        public IEnumerable<Chat> GetChats()
        {
            var chats = new List<Chat>();

            using (var connection = new SqliteConnection(_dbPath))
            {
                connection.Open();

                // Load all Chats
                var chatCommand = connection.CreateCommand();
                chatCommand.CommandText = "SELECT Id, Title FROM Chats";

                using (var reader = chatCommand.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        chats.Add(new Chat
                        {
                            Id = Guid.Parse(reader.GetString(0)),
                            Title = reader.GetString(1),
                            Messages = new ObservableCollection<ChatMessage>()
                        });
                    }
                }

                // Load Messages for each Chat
                foreach (var chat in chats)
                {
                    var msgCommand = connection.CreateCommand();
                    msgCommand.CommandText = "SELECT Text, IsMyMessage, Timestamp FROM Messages WHERE ChatId = $chatId ORDER BY Timestamp ASC";
                    msgCommand.Parameters.AddWithValue("$chatId", chat.Id.ToString());

                    using (var msgReader = msgCommand.ExecuteReader())
                    {
                        while (msgReader.Read())
                        {
                            var msg = new ChatMessage(msgReader.GetString(0), msgReader.GetBoolean(1))
                            {
                                // Parse the ISO string back to DateTime
                                Timestamp = DateTime.Parse(msgReader.GetString(2))
                            };
                            chat.Messages.Add(msg);
                        }
                    }
                }
            }

            return chats;
        }

        public void SaveChat(Chat chat)
        {
            using (var connection = new SqliteConnection(_dbPath))
            {
                connection.Open();

                using (var transaction = connection.BeginTransaction())
                {
                    // Upsert the Chat
                    var chatCommand = connection.CreateCommand();
                    chatCommand.CommandText = @"
                        INSERT INTO Chats (Id, Title) 
                        VALUES ($id, $title) 
                        ON CONFLICT(Id) DO UPDATE SET Title = $title;
                    ";
                    chatCommand.Parameters.AddWithValue("$id", chat.Id.ToString());
                    chatCommand.Parameters.AddWithValue("$title", chat.Title ?? "New Conversation");
                    chatCommand.ExecuteNonQuery();

                    // Sync Messages
                    var deleteMsgCommand = connection.CreateCommand();
                    deleteMsgCommand.CommandText = "DELETE FROM Messages WHERE ChatId = $chatId";
                    deleteMsgCommand.Parameters.AddWithValue("$chatId", chat.Id.ToString());
                    deleteMsgCommand.ExecuteNonQuery();

                    // Insert current messages
                    foreach (var msg in chat.Messages)
                    {
                        var insertMsgCommand = connection.CreateCommand();
                        insertMsgCommand.CommandText = @"
                            INSERT INTO Messages (ChatId, Text, IsMyMessage, Timestamp)
                            VALUES ($chatId, $text, $isMyMessage, $timestamp)
                        ";
                        insertMsgCommand.Parameters.AddWithValue("$chatId", chat.Id.ToString());
                        insertMsgCommand.Parameters.AddWithValue("$text", msg.Text ?? string.Empty);
                        insertMsgCommand.Parameters.AddWithValue("$isMyMessage", msg.IsMyMessage ? 1 : 0);

                        // Save DateTime in a sortable ISO 8601 format
                        insertMsgCommand.Parameters.AddWithValue("$timestamp", msg.Timestamp.ToString("O"));

                        insertMsgCommand.ExecuteNonQuery();
                    }

                    transaction.Commit();
                }
            }
        }

        public void DeleteChat(Chat chat)
        {
            using (var connection = new SqliteConnection(_dbPath))
            {
                connection.Open();

                var command = connection.CreateCommand();
                // Manually delete child messages first, then the chat to respect foreign key constraints
                command.CommandText = @"
                    DELETE FROM Messages WHERE ChatId = $id;
                    DELETE FROM Chats WHERE Id = $id;
                ";
                command.Parameters.AddWithValue("$id", chat.Id.ToString());
                command.ExecuteNonQuery();
            }
        }
    }
}