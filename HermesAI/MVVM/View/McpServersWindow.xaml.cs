using HermesAI.MVVM.Services;
using HermesAI.MVVM.ViewModel;
using System.Windows;
using System.Windows.Input;

namespace HermesAI.MVVM.View
{
    public partial class McpServersWindow : Window
    {
        /// <summary>
        /// Initializes the MCP Servers window and binds its DataContext to a new ViewModel.
        /// </summary>
        public McpServersWindow(McpClientManager mcpManager)
        {
            InitializeComponent();
            DataContext = new McpServersViewModel(mcpManager);
        }

        /// <summary>
        /// Enables dragging the window around when clicking on the custom title bar.
        /// </summary>
        private void TitleBar_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
                DragMove();
        }
    }
}
