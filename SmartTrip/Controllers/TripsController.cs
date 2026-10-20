using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SmartTrip.Data;
using SmartTrip.Models;

namespace SmartTrip.Controllers
{
    public class TripsController : Controller
    {
        
        private readonly AppDbContext _context; // осигурва връзката с БД само за този контролер.

        // Прави се връзката на контролера с БД
        public TripsController(AppDbContext context)
        {
            _context = context;
        }

        // 1. НАЧАЛНА СТРАНИЦА НА ПЪТУВАНИЯТА (GET: /Trips/)
        public async Task<IActionResult> Index()
        {
            // Взимаме всички пътувания от базата данни като списък
            var trips = await _context.Trips.ToListAsync();

            // Връщаме ги към HTML страницата (View)
            return View();
        }
    }
}