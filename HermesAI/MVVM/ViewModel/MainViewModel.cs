using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HermesAI.MVVM.Model;
using HermesAI.MVVM.Services;
using System.Collections;
using System.Windows;

namespace HermesAI
{
    public partial class MainViewModel : ObservableObject
    {
        [ObservableProperty]
        private string _inputText = string.Empty;

        [ObservableProperty]
        private Chat _currentChat = new Chat();

        [ObservableProperty]
        private IEnumerable<Chat> _chatList;

        private IChatRepository _chatRepository = new ChatRepository();

        public MainViewModel()
        {
            ChatList = _chatRepository.GetChats();

            if (ChatList != null && ChatList.Any())
            {
                CurrentChat = ChatList.First();
            }
        }

        [RelayCommand]
        private void SendMessage()
        {
            if (string.IsNullOrWhiteSpace(InputText)) return;
                    
            CurrentChat.Messages.Add(new ChatMessage(InputText, true));

            InputText = string.Empty;

            CurrentChat.Messages.Add(new ChatMessage("Verstanden. Ich kontaktiere den MCP-Server...", false));
        }

        [RelayCommand]
        private void SelectChat(Chat selectedChat)
        {
            if (selectedChat != null)
            {
                CurrentChat = selectedChat;
            }
        }

        [RelayCommand]
        private void CloseWindow(Window window)
        {
            window?.Close();
        }

        [RelayCommand]
        private void DragWindow(Window window)
        {
            if (Application.Current.MainWindow.WindowState == WindowState.Normal)
            {
                window?.DragMove();
            }
        }
    }
}