using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SmartTrip.Data;
using SmartTrip.Models;
using SmartTrip.Services;

namespace SmartTrip.Controllers
{
    public class TripsController : Controller
    {
        private readonly AppDbContext _context;
        private readonly WeatherService _weatherService;

        // Конструктор за инжектиране на зависимостите
        public TripsController(AppDbContext context, WeatherService weatherService)
        {
            _context = context;
            _weatherService = weatherService;
        }

        // 1. Списък с пътувания (взима времето на живо за всяко от тях)
        public async Task<IActionResult> Index()
        {
            var trips = await _context.Trips.ToListAsync();

            foreach (var trip in trips)
            {
                if (!string.IsNullOrEmpty(trip.City))
                {
                    trip.WeatherInfo = await _weatherService.GetWeatherAsync(trip.City);
                }
            }

            return View(trips);
        }

        // 2. Детайли за конкретно пътуване
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var trip = await _context.Trips.FirstOrDefaultAsync(m => m.Id == id);
            if (trip == null)
            {
                return NotFound();
            }

            if (!string.IsNullOrEmpty(trip.City))
            {
                trip.WeatherInfo = await _weatherService.GetWeatherAsync(trip.City);
            }

            return View(trip);
        }

        // 3. Създаване на ново пътуване
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(string destination, string budget, List<string> travelTypes, string interests)
        {
            string combinedTravelTypes = travelTypes != null && travelTypes.Count > 0
                ? string.Join(", ", travelTypes)
                : "Общ туризъм";

            string weatherInfo = await _weatherService.GetWeatherAsync(destination);

            var trip = new Trip
            {
                City = destination,
                Budget = budget,
                TravelTypes = combinedTravelTypes,
                Interests = interests,
                WeatherInfo = weatherInfo,
                CreatedDate = DateTime.Now
            };

            _context.Trips.Add(trip);
            await _context.SaveChangesAsync();

            return RedirectToAction(nameof(Index));
        }

        // 4. Изтриване на пътуване
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var trip = await _context.Trips.FindAsync(id);
            if (trip != null)
            {
                _context.Trips.Remove(trip);
                await _context.SaveChangesAsync();
            }

            return RedirectToAction(nameof(Index));
        }
    }
}