using System.ComponentModel.DataAnnotations;

namespace SmartTrip.Models
{
    public class Destination
    {
        public int Id { get; set; }

        [Required]
        public string Name { get; set; } 

        public string Address { get; set; } 

        public string ImageUrl { get; set; } 

        public double Latitude { get; set; } 
        public double Longitude { get; set; } 

        // Връзка с пътуването
        public int TripId { get; set; }
        public Trip Trip { get; set; }
    }
}