using map_creator.Models;
using Microsoft.Data.Sqlite;
using System;
using System.IO;
using System.Collections.Generic;

namespace map_creator.Services
{
    public class MapService
    {
        private readonly string _connectionString;
        private readonly string _dbPath;
        private readonly string _cs;

        public MapService(string dbPath)
        {
            if (string.IsNullOrWhiteSpace(dbPath))
                throw new ArgumentException("dbPath is empty");

            _connectionString = $"Data Source={dbPath}";

            _dbPath = dbPath;
            _cs = $"Data Source={_dbPath}";
        }
        

        /// <summary>
        /// Zapisuje mapę do DB.
        /// - Jeśli record.Id == 0 => INSERT i zwraca nowe Id
        /// - Jeśli record.Id > 0  => UPDATE i zwraca to samo Id
        /// </summary>
        public int Save(MapRecord record)
        {
            Validate(record);

            using var con = new SqliteConnection(_connectionString);
            con.Open();

            using var tx = con.BeginTransaction();

            if (record.Id <= 0)
            {
                using var cmd = con.CreateCommand();
                cmd.Transaction = tx;

                cmd.CommandText = @"
INSERT INTO Maps (NameMap, UserId, Date, Plus, Minus, ObjectJson, MapsJson, Desc)
VALUES ($name, $userId, $date, $plus, $minus, $objJson, $mapJson, $desc);
SELECT last_insert_rowid();
";
                cmd.Parameters.AddWithValue("$name", record.NameMap);
                cmd.Parameters.AddWithValue("$userId", record.UserId);
                cmd.Parameters.AddWithValue("$date", record.Date);
                cmd.Parameters.AddWithValue("$plus", record.Plus);
                cmd.Parameters.AddWithValue("$minus", record.Minus);
                cmd.Parameters.AddWithValue("$objJson", record.ObjectJson);
                cmd.Parameters.AddWithValue("$mapJson", record.MapsJson);
                cmd.Parameters.AddWithValue("$desc", record.Desc);

                var newId = Convert.ToInt32(cmd.ExecuteScalar());
                tx.Commit();
                return newId;
            }
            else
            {
                using var cmd = con.CreateCommand();
                cmd.Transaction = tx;

                cmd.CommandText = @"
UPDATE Maps
SET NameMap = $name,
    UserId = $userId,
    Date = $date,
    Plus = $plus,
    Minus = $minus,
    ObjectJson = $objJson,
    MapsJson = $mapJson,
    Desc = $desc
WHERE Id = $id;
";
                cmd.Parameters.AddWithValue("$id", record.Id);
                cmd.Parameters.AddWithValue("$name", record.NameMap);
                cmd.Parameters.AddWithValue("$userId", record.UserId);
                cmd.Parameters.AddWithValue("$date", record.Date);
                cmd.Parameters.AddWithValue("$plus", record.Plus);
                cmd.Parameters.AddWithValue("$minus", record.Minus);
                cmd.Parameters.AddWithValue("$objJson", record.ObjectJson);
                cmd.Parameters.AddWithValue("$mapJson", record.MapsJson);
                cmd.Parameters.AddWithValue("$desc", record.Desc);

                cmd.ExecuteNonQuery();
                tx.Commit();
                return record.Id;
            }
        }

        private static void Validate(MapRecord r)
        {
            if (r == null) throw new ArgumentNullException(nameof(r));

            // kolumny NOT NULL => nie mogą być null
            r.NameMap ??= "";
            r.UserId ??= "";
            r.Date ??= "";
            r.ObjectJson ??= "{}";
            r.MapsJson ??= "{}";
            r.Desc ??= "";

            if (string.IsNullOrWhiteSpace(r.NameMap))
                throw new ArgumentException("NameMap is empty");

            if (string.IsNullOrWhiteSpace(r.UserId))
                throw new ArgumentException("UserId is empty");

            if (string.IsNullOrWhiteSpace(r.Date))
                r.Date = DateTime.UtcNow.ToString("o");
        }

        private SqliteConnection Open()
        {
            if (!File.Exists(_dbPath))
                throw new FileNotFoundException("Nie znaleziono pliku bazy danych:", _dbPath);

            var con = new SqliteConnection(_cs);
            con.Open();
            return con;
        }

        // ====== MAPS: LISTA ======
        public List<MapRow> GetAllMaps()
        {
            var list = new List<MapRow>();

            using var con = Open();
            using var cmd = con.CreateCommand();
            cmd.CommandText = @"
SELECT Id, NameMap, UserId, Date, Plus, Minus, ObjectJson, MapsJson, Desc
FROM Maps
ORDER BY Id DESC;
";

            using var r = cmd.ExecuteReader();
            while (r.Read())
            {
                list.Add(new MapRow
                {
                    Id = r.GetInt32(0),
                    NameMap = r.IsDBNull(1) ? "" : r.GetString(1),
                    UserId = r.IsDBNull(2) ? "" : r.GetString(2),
                    Date = r.IsDBNull(3) ? "" : r.GetString(3),
                    Plus = r.IsDBNull(4) ? 0 : r.GetInt32(4),
                    Minus = r.IsDBNull(5) ? 0 : r.GetInt32(5),
                    ObjectJson = r.IsDBNull(6) ? "" : r.GetString(6),
                    MapsJson = r.IsDBNull(7) ? "" : r.GetString(7),
                    Desc = r.IsDBNull(8) ? "" : r.GetString(8),
                });
            }

            return list;
        }

        // ====== SAVEMAPS: CZY ZAPISANA ======
        public bool IsSaved(string userId, int mapId)
        {
            using var con = Open();
            using var cmd = con.CreateCommand();
            cmd.CommandText = @"SELECT COUNT(*) FROM SaveMaps WHERE UserID = $u AND MapID = $m;";
            cmd.Parameters.AddWithValue("$u", userId);
            cmd.Parameters.AddWithValue("$m", mapId);

            var count = Convert.ToInt32(cmd.ExecuteScalar());
            return count > 0;
        }

        // ====== SAVEMAPS: ZAPISZ ======
        public void SaveToUser(string userId, int mapId)
        {
            using var con = Open();
            using var tx = con.BeginTransaction();

            // nie duplikuj
            using (var check = con.CreateCommand())
            {
                check.Transaction = tx;
                check.CommandText = @"SELECT COUNT(*) FROM SaveMaps WHERE UserID = $u AND MapID = $m;";
                check.Parameters.AddWithValue("$u", userId);
                check.Parameters.AddWithValue("$m", mapId);

                var cnt = Convert.ToInt32(check.ExecuteScalar());
                if (cnt > 0)
                {
                    tx.Commit();
                    return;
                }
            }

            using (var cmd = con.CreateCommand())
            {
                cmd.Transaction = tx;
                cmd.CommandText = @"INSERT INTO SaveMaps (UserID, MapID) VALUES ($u, $m);";
                cmd.Parameters.AddWithValue("$u", userId);
                cmd.Parameters.AddWithValue("$m", mapId);

                var rows = cmd.ExecuteNonQuery();
                if (rows != 1) throw new Exception("Nie udało się dodać wpisu do SaveMaps.");
            }

            tx.Commit();
        }

        // ====== SAVEMAPS: USUŃ ZAPIS ======
        public void UnsaveFromUser(string userId, int mapId)
        {
            using var con = Open();
            using var cmd = con.CreateCommand();
            cmd.CommandText = @"DELETE FROM SaveMaps WHERE UserID = $u AND MapID = $m;";
            cmd.Parameters.AddWithValue("$u", userId);
            cmd.Parameters.AddWithValue("$m", mapId);
            cmd.ExecuteNonQuery();
        }

        public class MapRow
        {
            public int Id { get; set; }
            public string NameMap { get; set; }
            public string UserId { get; set; }
            public string Date { get; set; }
            public int Plus { get; set; }
            public int Minus { get; set; }
            public string ObjectJson { get; set; }
            public string MapsJson { get; set; }
            public string Desc { get; set; }
        }
    }
}
