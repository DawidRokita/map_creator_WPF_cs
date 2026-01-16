using map_creator.Data;
using map_creator.Data.Models;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace map_creator.Services
{
    public class MapEfService
    {
        private readonly string _dbPath;

        public MapEfService(string dbPath)
        {
            _dbPath = dbPath;
        }

        public int InsertMap(string userId, string nameMap, string desc, string mapsJson, string objectJson)
        {
            using var db = DbContextFactory.Create(_dbPath);

            var map = new Map
            {
                NameMap = nameMap,
                UserId = userId,
                Date = DateTime.UtcNow,
                Plus = 0,
                Minus = 0,
                MapsJson = mapsJson,
                ObjectJson = objectJson,
                Desc = desc
            };

            db.Maps.Add(map);
            db.SaveChanges();
            return map.Id;
        }

        public List<Map> GetAllMaps()
        {
            using var db = DbContextFactory.Create(_dbPath);
            return db.Maps
                .OrderByDescending(m => m.Id)
                .ToList();
        }

        public bool SaveMapForUser(string userId, int mapId)
        {
            using var db = DbContextFactory.Create(_dbPath);

            bool already = db.SaveMaps.Any(x => x.UserID == userId && x.MapID == mapId);
            if (already) return false;

            db.SaveMaps.Add(new SaveMap
            {
                UserID = userId,
                MapID = mapId
            });

            db.SaveChanges();
            return true;
        }

        public List<int> GetSavedMapIds(string userId)
        {
            using var db = DbContextFactory.Create(_dbPath);
            return db.SaveMaps
                .Where(x => x.UserID == userId)
                .Select(x => x.MapID)
                .ToList();
        }
    }
}
