using System.Windows;

namespace HermesAI.MVVM.View
{
    public partial class RenameWindow : Window
    {
        public string NewTitle { get; private set; }

        public RenameWindow(string currentTitle)
        {
            InitializeComponent();
            InputTextBox.Text = currentTitle;
            InputTextBox.SelectAll();
            InputTextBox.Focus();
        }

        private void Save_Click(object sender, RoutedEventArgs e)
        {
            NewTitle = InputTextBox.Text;
            DialogResult = true;
            Close();
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}
