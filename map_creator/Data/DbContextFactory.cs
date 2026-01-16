using Microsoft.EntityFrameworkCore;
using map_creator.Data;

namespace map_creator.Data
{
    public static class DbContextFactory
    {
        public static AppDbContext Create(string dbPath)
        {
            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseSqlite($"Data Source={dbPath}")
                .Options;

            return new AppDbContext(options);
        }
    }
}
