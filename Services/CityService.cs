using AlexAssistant.Models;
using CsvHelper;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;

namespace AlexAssistant.Services
{
    public class CityService
    {
        private readonly string _csvFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "worldcities.csv");

        public List<WorldCity> GetCity(string searchText)
        {
            try
            {
                using (var reader = new StreamReader(_csvFilePath, Encoding.UTF8))
                using (var csv = new CsvReader(reader, CultureInfo.InvariantCulture))
                {
                    var records = csv.GetRecords<CityOnly>().ToList();

                    return records
                        .Where(c => c.city.Contains(searchText, StringComparison.OrdinalIgnoreCase))
                        .Take(3)
                        .Select(c => new WorldCity
                        {
                            City = c.city,
                            Country = c.country
                        })
                        .ToList();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error reading CSV: {ex.Message}");
                return new List<WorldCity>();
            }
        }


    }
}
