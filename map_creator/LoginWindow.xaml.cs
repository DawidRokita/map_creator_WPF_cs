using System.Windows;

namespace map_creator
{
    public partial class LoginWindow : Window
    {
        public LoginWindow()
        {
            InitializeComponent();
        }

        private void LoginButton_Click(object sender, RoutedEventArgs e)
        {
            string username = UsernameTextBox.Text.Trim();
            string password = PasswordBox.Password.Trim();

            if (username.Length == 0 || password.Length == 0)
            {
                MessageBox.Show("Podaj nazwę użytkownika i hasło.", "Logowanie", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            OpenMainWindow();
        }

        private void SkipLoginButton_Click(object sender, RoutedEventArgs e)
        {
            OpenMainWindow();
        }

        private void ExitButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void OpenMainWindow()
        {
            var mainWindow = new MainWindow();
            mainWindow.Show();
            this.Close();
        }
    }
}
