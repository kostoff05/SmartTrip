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
        private readonly AiService _aiService;
        private readonly OpenStreetMapService _mapService;
        private readonly CurrencyService _currencyService;

        public TripsController(
            AppDbContext context,
            WeatherService weatherService,
            AiService aiService,
            OpenStreetMapService mapService,
            CurrencyService currencyService)
        {
            _context = context;
            _weatherService = weatherService;
            _aiService = aiService;
            _mapService = mapService;
            _currencyService = currencyService;
        }

        // ==========================================
        // 1. ВСИЧКИ ПЪТУВАНИЯ
        // ==========================================

        public async Task<IActionResult> Index()
        {
            var trips = await _context.Trips
                .OrderByDescending(t => t.CreatedDate)
                .ToListAsync();

            return View(trips);
        }

        // ==========================================
        // 2. ДЕТАЙЛИ ЗА ПЪТУВАНЕ
        // ==========================================

        public async Task<IActionResult> Details(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var trip = await _context.Trips
                .AsNoTracking()
                .FirstOrDefaultAsync(t => t.Id == id.Value);

            if (trip == null)
            {
                return NotFound();
            }

            // Показваме записаната информация веднага.
            // Не чакаме Weather API или OpenStreetMap.

            ViewBag.Weather = !string.IsNullOrWhiteSpace(trip.WeatherInfo)
                ? trip.WeatherInfo
                : "Зареждане на времето...";

            ViewBag.TripWeather = trip.TripWeatherInfo;

            ViewBag.Restaurants = new List<Restaurant>();

            return View(trip);
        }

        // ==========================================
        // 3. ТЕКУЩО ВРЕМЕ - ОТДЕЛНА ЗАЯВКА
        // ==========================================

        [HttpGet]
        public async Task<IActionResult> CurrentWeather(int id)
        {
            var trip = await _context.Trips
                .AsNoTracking()
                .FirstOrDefaultAsync(t => t.Id == id);

            if (trip == null)
            {
                return NotFound();
            }

            try
            {
                var weather = await _weatherService
                    .GetWeatherAsync(trip.City);

                return Json(new
                {
                    success = true,
                    weather
                });
            }
            catch
            {
                return Json(new
                {
                    success = false,
                    weather = "Няма налични данни за времето."
                });
            }
        }

        // ==========================================
        // ВАЛУТА НА ДЕСТИНАЦИЯТА
        // ==========================================

        [HttpGet]
        public async Task<IActionResult> DestinationCurrency(int id)
        {
            var trip = await _context.Trips
                .AsNoTracking()
                .FirstOrDefaultAsync(t => t.Id == id);

            if (trip == null)
                return NotFound();

            var currency = await _currencyService
                .GetCurrencyAsync(trip.City);

            if (currency == null)
            {
                return Json(new
                {
                    success = false,
                    message = "Не успяхме да намерим валутата на дестинацията."
                });
            }

            return Json(new
            {
                success = true,
                country = currency.CountryName,
                currencyName = currency.CurrencyName,
                currencyCode = currency.CurrencyCode,
                symbol = currency.Symbol
            });
        }

        // ==========================================
        // 4. РЕСТОРАНТИ - ОТДЕЛНА ЗАЯВКА
        // ==========================================

        [HttpGet]
        public async Task<IActionResult> Restaurants(int id)
        {
            var trip = await _context.Trips
                .AsNoTracking()
                .FirstOrDefaultAsync(t => t.Id == id);

            if (trip == null)
            {
                return NotFound();
            }

            try
            {
                var restaurants = await _mapService
                    .GetRestaurantsAsync(trip.City);

                return Json(new
                {
                    success = true,
                    restaurants = restaurants
                        .Take(6)
                        .Select(r => new
                        {
                            name = r.Name,
                            cuisine = r.Cuisine,
                            mapUrl = r.MapUrl
                        })
                        .ToList()
                });
            }
            catch
            {
                return Json(new
                {
                    success = false,
                    restaurants = Array.Empty<object>()
                });
            }
        }

        // ==========================================
        // 5. СЪЗДАВАНЕ - GET
        // ==========================================

        [HttpGet]
        public IActionResult Create()
        {
            return RedirectToAction("Index", "Home");
        }

        // ==========================================
        // 6. СЪЗДАВАНЕ - POST
        // ==========================================

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(
            string destination,
            DateTime startDate,
            DateTime endDate,
            string budget,
            int travelers,
            string travelType,
            List<string> travelTypes)
        {
            // Проверяваме дали заявката е изпратена чрез JavaScript.
            bool isAjaxRequest =
                Request.Headers["X-Requested-With"] == "XMLHttpRequest";

            // ==========================================
            // ПРОВЕРКА НА ДЕСТИНАЦИЯТА
            // ==========================================

            if (string.IsNullOrWhiteSpace(destination))
            {
                return CreateError(
                    "Моля, въведете дестинация.",
                    isAjaxRequest);
            }

            // ==========================================
            // ПРОВЕРКА НА ДАТИТЕ
            // ==========================================

            if (startDate == default || endDate == default)
            {
                return CreateError(
                    "Моля, изберете начална и крайна дата.",
                    isAjaxRequest);
            }

            if (endDate.Date < startDate.Date)
            {
                return CreateError(
                    "Крайната дата не може да бъде преди началната.",
                    isAjaxRequest);
            }

            int totalDays =
                (endDate.Date - startDate.Date).Days + 1;

            if (totalDays > 14)
            {
                return CreateError(
                    "Моля, изберете пътуване до 14 дни, " +
                    "за да генерираме подробен маршрут.",
                    isAjaxRequest);
            }

            // ==========================================
            // ПРОВЕРКА НА БЮДЖЕТА
            // ==========================================

            if (!decimal.TryParse(
                    budget,
                    System.Globalization.NumberStyles.Number,
                    System.Globalization.CultureInfo.InvariantCulture,
                    out decimal budgetValue) ||
                budgetValue <= 0)
            {
                return CreateError(
                    "Моля, въведете валиден бюджет.",
                    isAjaxRequest);
            }

            if (travelers < 1)
            {
                travelers = 1;
            }

            // ==========================================
            // ОБЕДИНЯВАНЕ НА ИНТЕРЕСИТЕ
            // ==========================================

            string selectedTravelTypes = travelTypes != null
                ? string.Join(", ", travelTypes)
                : string.Empty;

            string allInterests = string.IsNullOrWhiteSpace(
                selectedTravelTypes)
                ? travelType ?? "Общи туристически интереси"
                : $"{travelType}, {selectedTravelTypes}";

            // ==========================================
            // 7. ПРОГНОЗА ЗА ПЪТУВАНЕТО
            // ==========================================

            string tripWeatherInfo;

            try
            {
                tripWeatherInfo = await _weatherService
                    .GetTripWeatherAsync(
                        destination,
                        startDate,
                        endDate);
            }
            catch
            {
                tripWeatherInfo =
                    "Няма налична прогноза за избрания период.";
            }

            // ==========================================
            // 8. GEMINI AI
            // ==========================================

            string aiRecommendation;

            try
            {
                aiRecommendation = await _aiService
                    .GenerateTripGuideAsync(
                        destination,
                        allInterests,
                        budget,
                        startDate,
                        endDate,
                        travelers,
                        tripWeatherInfo);
            }
            catch
            {
                aiRecommendation =
                    "<p>Не успяхме да генерираме " +
                    "персонализиран пътеводител.</p>";
            }

            // ==========================================
            // 9. ЗАПИСВАНЕ В SQLITE
            // ==========================================

            var trip = new Trip
            {
                City = destination.Trim(),

                Budget = budget,

                TravelType = travelType,

                TravelTypes = selectedTravelTypes,

                Interests = allInterests,

                StartDate = startDate.Date,

                EndDate = endDate.Date,

                Travelers = travelers,

                TripWeatherInfo = tripWeatherInfo,

                AiRecommendation = aiRecommendation,

                CreatedDate = DateTime.Now
            };

            _context.Trips.Add(trip);

            await _context.SaveChangesAsync();

            // ==========================================
            // 10. ПРЕНАСОЧВАНЕ КЪМ НОВОТО ПЪТУВАНЕ
            // ==========================================

            string detailsUrl = Url.Action(
                nameof(Details),
                "Trips",
                new { id = trip.Id }
            ) ?? $"/Trips/Details/{trip.Id}";

            // Ако заявката идва от JavaScript,
            // връщаме JSON с адреса на новото пътуване.
            if (isAjaxRequest)
            {
                return Json(new
                {
                    success = true,
                    id = trip.Id,
                    url = detailsUrl
                });
            }

            // При стандартно изпращане на формата
            // използваме нормално пренасочване.
            return RedirectToAction(
                nameof(Details),
                new { id = trip.Id });
        }

        // ==========================================
        // 11. ОБРАБОТКА НА ГРЕШКИ ПРИ СЪЗДАВАНЕ
        // ==========================================

        private IActionResult CreateError(
            string message,
            bool isAjaxRequest)
        {
            if (isAjaxRequest)
            {
                return BadRequest(new
                {
                    success = false,
                    message
                });
            }

            TempData["Error"] = message;

            return RedirectToAction("Index", "Home");
        }

        // ==========================================
        // 12. ИЗТРИВАНЕ - GET
        // ==========================================

        [HttpGet]
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var trip = await _context.Trips
                .FirstOrDefaultAsync(t => t.Id == id.Value);

            if (trip == null)
            {
                return NotFound();
            }

            return View(trip);
        }

        // ==========================================
        // 13. ИЗТРИВАНЕ - POST
        // ==========================================

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