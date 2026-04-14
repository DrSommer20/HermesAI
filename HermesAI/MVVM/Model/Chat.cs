using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Text;

namespace HermesAI.MVVM.Model
{
    public class Chat
    {
        public ObservableCollection<ChatMessage> Messages { get; set; }

        public string Title { get; set; }

        public Chat()
        {
            Messages = new ObservableCollection<ChatMessage>();
            Title = "New Conversation";
        }
    }
}
