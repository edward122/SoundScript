using System.Windows;

namespace SoundScript.Views
{
    public partial class TextConfirmationDialog : Window
    {
        private readonly string _requiredText;

        public TextConfirmationDialog(string requiredText)
        {
            InitializeComponent();
            _requiredText = requiredText;
            InstructionText.Text = $"Type '{requiredText}' to confirm:";
            
            // Enable dark mode for title bar
            SoundScript.Utils.DarkModeHelper.EnableDarkMode(this);
            
            // Focus the text box
            ConfirmationTextBox.Focus();
            
            // Handle Enter key
            ConfirmationTextBox.KeyDown += ConfirmationTextBox_KeyDown;
        }

        private void ConfirmationTextBox_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
        {
            ConfirmButton.IsEnabled = ConfirmationTextBox.Text.Trim().Equals(_requiredText, System.StringComparison.OrdinalIgnoreCase);
        }

        private void ConfirmButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
            Close();
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private void ConfirmationTextBox_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == System.Windows.Input.Key.Enter && ConfirmButton.IsEnabled)
            {
                DialogResult = true;
                Close();
            }
        }
    }
} 