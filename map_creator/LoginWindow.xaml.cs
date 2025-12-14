using map_creator.Services;
using map_creator.Session;
using System.IO;
using System.Windows;
using map_creator.Models;
using map_creator.Services;


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
            var auth = new AuthService(
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "BazaDanych.db")
            );

            if (!auth.Login(
                UsernameTextBox.Text.Trim(),
                PasswordBox.Password.Trim(),
                out var user))
            {
                MessageBox.Show("Błędny login lub hasło");
                return;
            }

            UserSession.Login(user);
            OpenMainWindow();
        }

        private void SkipLoginButton_Click(object sender, RoutedEventArgs e)
        {
            // brak logowania = brak UserSession
            OpenMainWindow();
        }

        private void RegisterButton_Click(object sender, RoutedEventArgs e)
        {
            var auth = new AuthService(
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "BazaDanych.db")
            );

            if (!auth.Register(
                UsernameTextBox.Text.Trim(),
                UsernameTextBox.Text.Trim() + "@mail.com", // albo osobne pole
                PasswordBox.Password.Trim(),
                out var error))
            {
                MessageBox.Show(error);
                return;
            }

            MessageBox.Show("Konto utworzone. Możesz się zalogować.");
        }

        private void PasswordBox_PasswordChanged(object sender, RoutedEventArgs e)
        {
            PasswordPlaceholder.Visibility =
                string.IsNullOrEmpty(PasswordBox.Password)
                ? Visibility.Visible
                : Visibility.Collapsed;
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
