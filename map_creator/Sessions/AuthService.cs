using Microsoft.AspNetCore.Identity;
using Microsoft.Data.Sqlite;
using map_creator.Models;

namespace map_creator.Services
{
    public class AuthService
    {
        private readonly string _connectionString;
        private readonly PasswordHasher<object> _hasher = new();

        public AuthService(string dbPath)
        {
            _connectionString = $"Data Source={dbPath}";
        }

        public bool Login(string username, string password, out LoggedUser user)
        {
            user = null;

            using var con = new SqliteConnection(_connectionString);
            con.Open();

            var cmd = con.CreateCommand();
            cmd.CommandText =
            """
            SELECT Id, UserName, Email, PasswordHash
            FROM AspNetUsers
            WHERE UserName = $u
            """;
            cmd.Parameters.AddWithValue("$u", username);

            using var r = cmd.ExecuteReader();
            if (!r.Read()) return false;

            var hash = r.GetString(3);

            var result = _hasher.VerifyHashedPassword(
                null,
                hash,
                password
            );

            if (result == PasswordVerificationResult.Failed)
                return false;

            user = new LoggedUser
            {
                Id = r.GetString(0),
                UserName = r.GetString(1),
                Email = r.IsDBNull(2) ? null : r.GetString(2)
            };

            return true;
        }

        public bool Register(string username, string email, string password, out string error)
        {
            error = null;

            using var con = new SqliteConnection(_connectionString);
            con.Open();

            // 1️⃣ sprawdź czy user istnieje
            var check = con.CreateCommand();
            check.CommandText =
            """
    SELECT COUNT(*) FROM AspNetUsers WHERE UserName = $u
    """;
            check.Parameters.AddWithValue("$u", username);

            long exists = (long)check.ExecuteScalar();
            if (exists > 0)
            {
                error = "Użytkownik już istnieje";
                return false;
            }

            // 2️⃣ przygotuj dane
            string id = Guid.NewGuid().ToString();
            string securityStamp = Guid.NewGuid().ToString();

            string passwordHash = _hasher.HashPassword(null, password);

            // 3️⃣ insert
            var cmd = con.CreateCommand();
            cmd.CommandText =
            """
    INSERT INTO AspNetUsers
    (
        Id,
        UserName,
        NormalizedUserName,
        Email,
        NormalizedEmail,
        EmailConfirmed,
        PasswordHash,
        SecurityStamp,
        PhoneNumberConfirmed,
        TwoFactorEnabled,
        LockoutEnabled,
        AccessFailedCount
    )
    VALUES
    (
        $id,
        $u,
        $un,
        $e,
        $en,
        1,
        $ph,
        $ss,
        0,
        0,
        0,
        0
    )
    
    """;

            cmd.Parameters.AddWithValue("$id", id);
            cmd.Parameters.AddWithValue("$u", username);
            cmd.Parameters.AddWithValue("$un", username.ToUpper());
            cmd.Parameters.AddWithValue("$e", email);
            cmd.Parameters.AddWithValue("$en", email?.ToUpper());
            cmd.Parameters.AddWithValue("$ph", passwordHash);
            cmd.Parameters.AddWithValue("$ss", securityStamp);

            cmd.ExecuteNonQuery();
            return true;
        }

    }
}
