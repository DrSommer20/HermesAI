using HermesAI.MVVM.Model;
using System;
using System.Collections.Generic;
using System.Data;
using System.Text;

namespace HermesAI.MVVM.Services
{
    class ChatRepository : IChatRepository
    {
        public ChatRepository() { }

        public IEnumerable<Chat> GetChats()
        {
            var dummyChats = new List<Chat>();

            // --- Dummy Chat 1 ---
            var chat1 = new Chat { Title = "MCP Server Setup" };
            chat1.Messages.Add(new ChatMessage("Wie verbinde ich meinen C#-Client mit MCP?", true));
            chat1.Messages.Add(new ChatMessage("Um einen MCP-Client in C# zu bauen, kannst du Standard I/O (stdio) oder HTTP verwenden. Was bevorzugst du?", false));
            chat1.Messages.Add(new ChatMessage("Lass uns stdio nehmen.", true));
            dummyChats.Add(chat1);

            // --- Dummy Chat 2 ---
            var chat2 = new Chat { Title = "WPF Styling Tipps" };
            chat2.Messages.Add(new ChatMessage("Wie mache ich abgerundete Ecken bei einem Button?", true));
            chat2.Messages.Add(new ChatMessage("Im klassischen WPF machst du das am besten über ein ControlTemplate und einen Border mit der Eigenschaft \'CornerRadius\'.", false));
            dummyChats.Add(chat2);

            // --- Dummy Chat 3 ---
            var chat3 = new Chat { Title = "Datenbank Design" };
            chat3.Messages.Add(new ChatMessage("Welche Datenbank eignet sich für lokale Desktop-Apps?", true));
            dummyChats.Add(chat3);

            return dummyChats;
        }
    }
}
