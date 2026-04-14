using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HermesAI.MVVM.Services;
using System.Windows;

namespace HermesAI.MVVM.ViewModel
{
    public partial class SettingsViewModel : ObservableObject
    {
        [ObservableProperty]
        private string? _apiKey;

        public SettingsViewModel()
        {
            ApiKey = SecretManager.LoadApiKey() ?? string.Empty;
        }

        [RelayCommand]
        private void SaveAndClose(Window window)
        {
            if (!string.IsNullOrWhiteSpace(ApiKey))
            {
                SecretManager.SaveApiKey(ApiKey);
                window?.Close();
            }
            else
            {
                MessageBox.Show("Bitte gib einen gültigen API-Key ein.");
            }
        }

        [RelayCommand]
        private void Cancel(Window window)
        {
            window?.Close();
        }
    }
}