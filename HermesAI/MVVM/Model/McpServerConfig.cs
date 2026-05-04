using CommunityToolkit.Mvvm.ComponentModel;

namespace HermesAI.MVVM.Model
{
    public partial class McpServerConfig : ObservableObject
    {
        [ObservableProperty]
        private string _name = "Neuer Server";

        [ObservableProperty]
        private string _transportType = "stdio"; // expecting "stdio" or "sse"

        // stdio transport specific
        [ObservableProperty]
        private string _command = "";

        [ObservableProperty]
        private string _arguments = ""; // space-separated args

        [ObservableProperty]
        private string? _workingDirectory;

        // sse transport specific
        [ObservableProperty]
        private string? _url;

        [ObservableProperty]
        private bool _isEnabled = true;
    }
}
