using Microsoft.Data.Sqlite;
using map_creator.Models;
using System;

namespace map_creator.Services
{
    public class MapService
    {
        private readonly string _connectionString;

        public MapService(string dbPath)
        {
            if (string.IsNullOrWhiteSpace(dbPath))
                throw new ArgumentException("dbPath is empty");

            _connectionString = $"Data Source={dbPath}";
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
    }
}
