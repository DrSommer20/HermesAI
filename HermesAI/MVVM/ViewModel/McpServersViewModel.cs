using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HermesAI.MVVM.Model;
using HermesAI.MVVM.Services;
using System.Collections.ObjectModel;
using System.Windows;

namespace HermesAI.MVVM.ViewModel
{
    public partial class McpServersViewModel : ObservableObject
    {
        private readonly McpClientManager _mcpManager;

        [ObservableProperty]
        private ObservableCollection<McpServerConfig> _servers = new();

        /// <summary>
        /// Currently selected server for editing (null means the editor is hidden).
        /// </summary>
        [ObservableProperty]
        private McpServerConfig? _editingServer;

        /// <summary>
        /// Controls whether the slide-in editor panel is visible.
        /// </summary>
        [ObservableProperty]
        private bool _isEditorVisible;

        /// <summary>
        /// Flag to track if we're creating a new server (true) or editing an existing one (false).
        /// </summary>
        [ObservableProperty]
        private bool _isNewServer;

        [ObservableProperty]
        private string _editorTitle = "Add Server";

        /// <summary>
        /// Initializes the ViewModel and loads the saved servers.
        /// </summary>
        public McpServersViewModel(McpClientManager mcpManager)
        {
            _mcpManager = mcpManager;
            LoadServers();
        }

        /// <summary>
        /// Loads the MCP server configs from disk into the observable collection.
        /// </summary>
        private void LoadServers()
        {
            var configs = _mcpManager.LoadConfigs();
            Servers = new ObservableCollection<McpServerConfig>(configs);
        }

        /// <summary>
        /// Persists the current list of servers to disk.
        /// </summary>
        private void SaveAll()
        {
            _mcpManager.SaveConfigs(Servers.ToList());
        }

        /// <summary>
        /// Opens the editor panel to create a brand new server config.
        /// </summary>
        [RelayCommand]
        private void AddServer()
        {
            var newConfig = new McpServerConfig();
            EditingServer = newConfig;
            IsNewServer = true;
            IsEditorVisible = true;
            EditorTitle = "Add Server";
        }

        /// <summary>
        /// Opens the editor panel to modify an existing server.
        /// </summary>
        [RelayCommand]
        private void EditServer(McpServerConfig server)
        {
            if (server == null) return;
            EditingServer = server;
            IsNewServer = false;
            IsEditorVisible = true;
            EditorTitle = "Edit Server";
        }

        /// <summary>
        /// Saves the changes from the editor and closes the panel.
        /// </summary>
        [RelayCommand]
        private void SaveEditor()
        {
            if (EditingServer == null) return;

            if (IsNewServer)
            {
                Servers.Add(EditingServer);
            }

            SaveAll();
            IsEditorVisible = false;
            EditingServer = null;
        }

        /// <summary>
        /// Cancels the editing process, reverting any unsaved changes.
        /// </summary>
        [RelayCommand]
        private void CancelEditor()
        {
            if (IsNewServer && EditingServer != null)
            {
                // Just toss out the new server, no big deal
            }
            else if (!IsNewServer && EditingServer != null)
            {
                // If the user bails out, we just reload configs to drop any unsaved changes
                LoadServers();
            }

            IsEditorVisible = false;
            EditingServer = null;
        }

        /// <summary>
        /// Toggles the enabled state of a server so it can be temporarily disabled.
        /// </summary>
        [RelayCommand]
        private void ToggleServer(McpServerConfig server)
        {
            if (server == null) return;
            server.IsEnabled = !server.IsEnabled;
            SaveAll();
        }

        /// <summary>
        /// Deletes a server from the list.
        /// </summary>
        [RelayCommand]
        private void DeleteServer(McpServerConfig server)
        {
            if (server == null) return;
            Servers.Remove(server);
            SaveAll();

            // Just in case they delete the server they're currently editing
            if (EditingServer == server)
            {
                IsEditorVisible = false;
                EditingServer = null;
            }
        }

        /// <summary>
        /// Closes the MCP servers window.
        /// </summary>
        [RelayCommand]
        private void CloseWindow(Window window)
        {
            window?.Close();
        }
    }
}
