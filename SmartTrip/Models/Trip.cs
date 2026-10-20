using System.ComponentModel.DataAnnotations;

namespace SmartTrip.Models
{
    public class Trip
    {
        public int Id { get; set; }

        [Required]
        public string City { get; set; } // Търсения град (напр. "Рим")

        public string Budget { get; set; } // Бюджет (Нисък, Среден, Висок)

        public string WeatherInfo { get; set; } // Информация за времето от API

        public string PackingList { get; set; } // Списък за багаж от AI

        public DateTime CreatedDate { get; set; } = DateTime.Now;

        // Списък със забележителности към това пътуване
        public List<Destination> Destinations { get; set; } = new List<Destination>();
    }
}