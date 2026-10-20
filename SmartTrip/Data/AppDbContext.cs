using Microsoft.EntityFrameworkCore;
using SmartTrip.Models;

namespace SmartTrip.Data
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
        {
        }

        // Създаваме таблиците в базата данни за нашите модели
        public DbSet<Trip> Trips { get; set; } // Тук се създава таблица Trips
        public DbSet<Destination> Destinations { get; set; } // Тук се създава таблица Destinations
    }
}