using System.ComponentModel.DataAnnotations;

namespace SmartTrip.Models
{
    public class Trip
    {
        public int Id { get; set; }

        [Required]
        public string City { get; set; } // Дестинацията (напр. "Рим")

        public string Budget { get; set; } // Бюджет

        public string? TravelType { get; set; } // Тип пътуване (Култура, Природа и т.н.)

        public string? Interests { get; set; } // Допълнителни интереси

        public DateTime CreatedDate { get; set; } = DateTime.Now;

        public string? WeatherInfo { get; set; }
        public List<Destination> Destinations { get; set; } = new List<Destination>();

        public string? TravelTypes { get; set; }
    }
}