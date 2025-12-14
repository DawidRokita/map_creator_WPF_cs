using map_creator.Models;

namespace map_creator.Session
{
    public static class UserSession
    {
        public static LoggedUser CurrentUser { get; private set; }

        public static bool IsLoggedIn => CurrentUser != null;

        public static void Login(LoggedUser user)
        {
            CurrentUser = user;
        }

        public static void Logout()
        {
            CurrentUser = null;
        }
    }
}
