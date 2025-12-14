using SQLitePCL;
using System.Windows;

namespace map_creator
{
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);
            Batteries.Init();
            var loginWindow = new LoginWindow();
            loginWindow.Show();
        }
    }
}
