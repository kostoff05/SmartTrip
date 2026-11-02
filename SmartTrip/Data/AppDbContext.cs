using Microsoft.EntityFrameworkCore;
using SmartTrip.Models;

namespace SmartTrip.Data
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
        {
        }

        // Таблиците в базата данни за нашите модели
        public DbSet<Trip> Trips { get; set; } 
        public DbSet<Destination> Destinations { get; set; } 
    }
}