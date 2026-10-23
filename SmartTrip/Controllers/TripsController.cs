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
        private readonly AiService _aiService; // Добавяме AI услугата

        public TripsController(AppDbContext context, WeatherService weatherService, AiService aiService)
        {
            _context = context;
            _weatherService = weatherService;
            _aiService = aiService;
        }

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

        public async Task<IActionResult> Details(int? id)
        {
            if (id == null) return NotFound();

            var trip = await _context.Trips.FirstOrDefaultAsync(m => m.Id == id);
            if (trip == null) return NotFound();

            if (!string.IsNullOrEmpty(trip.City))
            {
                trip.WeatherInfo = await _weatherService.GetWeatherAsync(trip.City);
            }

            return View(trip);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(string destination, string budget, List<string> travelTypes, string interests)
        {
            string combinedTravelTypes = travelTypes != null && travelTypes.Count > 0
                ? string.Join(", ", travelTypes)
                : "Общ туризъм";

            string weatherInfo = await _weatherService.GetWeatherAsync(destination);

            // Генерираме AI съвет за дестинацията
            string aiRecommendation = await _aiService.GenerateTravelTipAsync(destination, combinedTravelTypes, budget);

            var trip = new Trip
            {
                City = destination,
                Budget = budget,
                TravelTypes = combinedTravelTypes,
                Interests = interests,
                WeatherInfo = weatherInfo,
                AiRecommendation = aiRecommendation, // Записваме съвета в базата данни
                CreatedDate = DateTime.Now
            };

            _context.Trips.Add(trip);
            await _context.SaveChangesAsync();

            return RedirectToAction(nameof(Index));
        }

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