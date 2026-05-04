using CommunityToolkit.Mvvm.ComponentModel;
using System;
using System.Collections.Generic;
using System.Text;

namespace HermesAI.MVVM.Model
{
    public partial class ChatMessage : ObservableObject
    {
        [ObservableProperty]
        private string _text;
        [ObservableProperty]
        private bool _isMyMessage;
        [ObservableProperty]
        private DateTime _timestamp;
        [ObservableProperty]
        private bool _isToolMessage;
        [ObservableProperty]
        private string? _toolName;

        public ChatMessage(string text, bool isMyMessage) {
            this.Text = text;
            this.IsMyMessage = isMyMessage; 
            _timestamp = DateTime.Now;
        }

        /// <summary>
        /// Creates a special ChatMessage that represents a message from a tool 
        /// </summary>
        public static ChatMessage CreateToolMessage(string toolName, string text)
        {
            return new ChatMessage(text, false)
            {
                IsToolMessage = true,
                ToolName = toolName
            };
        }
    }
}
