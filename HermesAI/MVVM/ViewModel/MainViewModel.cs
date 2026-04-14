using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HermesAI.MVVM.Model;
using HermesAI.MVVM.Services;
using HermesAI.MVVM.View;
using System.Collections;
using System.Windows;

namespace HermesAI.MVVM.ViewModel
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

        private readonly IAIConnection _aiConnection = new GeminiConnection();

        public MainViewModel()
        {
            ChatList = _chatRepository.GetChats();

            if (ChatList != null && ChatList.Any())
            {
                CurrentChat = ChatList.First();
            }
        }

        [RelayCommand]
        private async Task SendMessageAsync()
        {
            if (string.IsNullOrWhiteSpace(InputText)) return;
                   
            CurrentChat.Messages.Add(new ChatMessage(InputText, true));
            InputText = string.Empty;
            var loadingMessage = new ChatMessage("...", false);
            CurrentChat.Messages.Add(loadingMessage);

            var historyForApi = CurrentChat.Messages.Where(m => m != loadingMessage).ToList();

            string aiResponse = await _aiConnection.GetResponseAsync(historyForApi);

            CurrentChat.Messages.Remove(loadingMessage);
            CurrentChat.Messages.Add(new ChatMessage(aiResponse, false));
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

        [RelayCommand]
        private void OpenSettings()
        {
            var settingsWin = new SettingsWindow();
            settingsWin.Owner = Application.Current.MainWindow;
            settingsWin.ShowDialog();
        }
    }
}