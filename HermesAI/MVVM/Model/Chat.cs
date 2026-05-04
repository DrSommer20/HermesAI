using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Text;
using CommunityToolkit.Mvvm.ComponentModel;

namespace HermesAI.MVVM.Model
{
    public partial class Chat : ObservableObject
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public ObservableCollection<ChatMessage> Messages { get; set; }
        
        [ObservableProperty]
        private string _title;
        
        public Chat()
        {
            Messages = new ObservableCollection<ChatMessage>();
            Title = "New Conversation";
        }
    }
}
