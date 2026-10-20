using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace SmartTrip.Data
{
    // 1. Създаваме клас, който казва: "Аз съм фабрика (производител) на AppDbContext за време на дизайн"
    public class AppDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
    {
        // 2. Това е методът, който инструментът за миграции извиква автоматично зад кулисите
        public AppDbContext CreateDbContext(string[] args)
        {
            // 3. Създаваме строител на опции (DbContextOptionsBuilder), точно както правим в Program.cs
            var optionsBuilder = new DbContextOptionsBuilder<AppDbContext>();

            // 4. Изрично му казваме: "Използвай SQLite и файл с име smarttrip.db"
            optionsBuilder.UseSqlite("Data Source=smarttrip.db");

            // 5. Създаваме и връщаме готов обект от нашия AppDbContext с тези настройки
            return new AppDbContext(optionsBuilder.Options);
        }
    }
}