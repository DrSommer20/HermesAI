    using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace HermesAI.MVVM.View
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        /// <summary>
        /// Initializes the main window and sets up the MainViewModel.
        /// </summary>
        public MainWindow()
        {
            DataContext = new ViewModel.MainViewModel();
            InitializeComponent();
        }

        /// <summary>
        /// Automatically scrolls the chat to the bottom when new messages arrive.
        /// </summary>
        private void ChatScrollViewer_ScrollChanged(object sender, ScrollChangedEventArgs e)
        {
            if (e.ExtentHeightChange > 0)
            {
                ChatScrollViewer.ScrollToBottom();
            }
        }

        /// <summary>
        /// Handles mouse wheel scrolling for the chat viewer, preventing child elements from swallowing the event.
        /// </summary>
        private void ChatScrollViewer_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            // Prevent child controls (MarkdownViewer's internal FlowDocumentScrollViewer) 
            // from swallowing the scroll event
            ChatScrollViewer.ScrollToVerticalOffset(ChatScrollViewer.VerticalOffset - e.Delta);
            e.Handled = true;
        }

        /// <summary>
        /// Handles the Enter key press in the input textbox to send a message, while allowing Shift+Enter for newlines.
        /// </summary>
        private void InputTextBox_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                if (Keyboard.Modifiers == ModifierKeys.Shift)
                {
                    // Allow normal behavior (new line)
                    return;
                }
                
                // Prevent new line
                e.Handled = true;
                
                // Execute SendMessageCommand
                if (DataContext as ViewModel.MainViewModel is var vm)
                {
                    if (vm?.SendMessageCommand.CanExecute(null) == true)
                    {
                        vm.SendMessageCommand.Execute(null);
                    }
                }
            }
        }
    }
}