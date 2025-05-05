using System.ComponentModel.DataAnnotations;

namespace AlexAssistant.Models
{
    public class WorldCity
    {

        public string City { get; set; }  
        public string? CityAscii { get; set; }
        public double? Latitude { get; set; }
        public double? Longitude { get; set; }
        public string? Country { get; set; }
        public string? ISO2 { get; set; }
        public string? AdminName { get; set; }
        public long? Population { get; set; }  
    }
}
