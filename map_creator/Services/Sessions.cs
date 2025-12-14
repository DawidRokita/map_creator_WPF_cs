using map_creator.Models;

namespace map_creator.Services
{
    public static class Session
    {
        public static LoggedUser CurrentUser { get; set; }

        public static bool IsLoggedIn => CurrentUser != null;

        public static void Logout()
        {
            CurrentUser = null;
        }
    }
}
