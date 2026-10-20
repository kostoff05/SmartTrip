using System.ComponentModel.DataAnnotations;

namespace SmartTrip.Models
{
    public class Destination
    {
        public int Id { get; set; }

        [Required]
        public string Name { get; set; } // Име на забележителността (напр. "Колизеум")

        public string Address { get; set; } // Адрес

        public string ImageUrl { get; set; } // Линк към снимка

        public double Latitude { get; set; } // Географска ширина за картата
        public double Longitude { get; set; } // Географска дължина за картата

        // Връзка с пътуването
        public int TripId { get; set; }
        public Trip Trip { get; set; }
    }
}