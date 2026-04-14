using System;
using System.Collections.Generic;
using System.Text;

namespace HermesAI.MVVM.Model
{
    public class ChatMessage
    {
        public string Text { get; set; }
        public bool IsMyMessage { get; set; }
        public DateTime Timestamp { get; set; }
     
        public ChatMessage(string text, bool isMyMessage) {
            this.Text = text;
            this.IsMyMessage = isMyMessage; 
            Timestamp = DateTime.Now;
        }
    }
}
