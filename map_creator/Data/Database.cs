using System.Data.SQLite;
using System.IO;

namespace map_creator.Data
{
    public static class Database
    {
        private static readonly string DbPath =
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "BazaDanych.db");

        public static SQLiteConnection GetConnection()
        {
            return new SQLiteConnection($"Data Source={DbPath};Version=3;");
        }
    }
}
