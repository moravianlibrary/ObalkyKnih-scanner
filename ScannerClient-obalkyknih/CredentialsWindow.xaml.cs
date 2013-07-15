using System;
using System.Windows;


namespace ScannerClient_obalkyknih
{
    /// <summary>
    /// Interaction logic for CredentialsWindow.xaml
    /// </summary>
    public partial class CredentialsWindow : Window
    {

        public CredentialsWindow()
        {
            InitializeComponent();
            LoadSettings();
        }

        // Loads credentials from settings file to window components
        private void LoadSettings()
        {
            this.userNameTextBox.Text = Settings.UserName;
            this.passwordBox.Password = Settings.Password;
        }

        // Saves credentials and if checkbox is checked, persists them to file
        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            Settings.UserName = this.userNameTextBox.Text;
            Settings.Password = this.passwordBox.Password;

            if ((bool) this.saveCredentialsCheckBox.IsChecked)
            {
                Settings.PersistSettings();
            }

            //close settings window
            Window parentWindow = Window.GetWindow(this);
            if (parentWindow != null)
            {
                parentWindow.Close();
            }
        }
    }
}
