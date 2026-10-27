using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace SmartTrip.Models
{
    public class Trip
    {
        public int Id { get; set; }

        [Required]
        public string City { get; set; } = string.Empty;

        public string Budget { get; set; } = string.Empty;

        public string? TravelType { get; set; }

        public string? Interests { get; set; }

        public DateTime CreatedDate { get; set; } = DateTime.Now;

        // Начална и крайна дата на пътуването
        public DateTime? StartDate { get; set; }

        public DateTime? EndDate { get; set; }

        // Брой пътуващи
        public int Travelers { get; set; } = 2;

        // Информация за текущото време
        public string? WeatherInfo { get; set; }

        // Прогноза за периода на пътуването
        public string? TripWeatherInfo { get; set; }

        // Персонализиран AI пътеводител,
        // включително маршрути по дни и часове
        public string? AiRecommendation { get; set; }

        public string? TravelTypes { get; set; }

        public List<Destination> Destinations { get; set; } =
            new List<Destination>();
    }
}