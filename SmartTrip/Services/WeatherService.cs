using System.Text.Json;

namespace SmartTrip.Services
{
    public class WeatherService
    {
        private readonly HttpClient _httpClient;

        public WeatherService(HttpClient httpClient)
        {
            _httpClient = httpClient;
        }

        public async Task<string> GetWeatherAsync(string city)
        {
            if (string.IsNullOrEmpty(city)) return "Няма информация";

            try
            {
                // Подаваме изписаното от потребителя име (на кирилица или латиница) директно към световната търсачка
                string trimmedCity = city.Trim();
                string geoUrl = $"https://geocoding-api.open-meteo.com/v1/search?name={Uri.EscapeDataString(trimmedCity)}&count=1&language=bg&format=json";
                var geoResponse = await _httpClient.GetAsync(geoUrl);

                if (!geoResponse.IsSuccessStatusCode) return "Грешка при връзка";

                var geoJson = await geoResponse.Content.ReadAsStringAsync();
                using var geoDoc = JsonDocument.Parse(geoJson);
                var root = geoDoc.RootElement;

                // Ако не намери резултат с български език, опитваме втори път без езиково ограничение (за по-добра разпознаваемост на чужди имена на кирилица)
                if (!root.TryGetProperty("results", out var results) || results.GetArrayLength() == 0)
                {
                    string fallbackGeoUrl = $"https://geocoding-api.open-meteo.com/v1/search?name={Uri.EscapeDataString(trimmedCity)}&count=1&format=json";
                    var fallbackResponse = await _httpClient.GetAsync(fallbackGeoUrl);
                    if (!fallbackResponse.IsSuccessStatusCode) return $"Градът не е намерен";

                    var fallbackJson = await fallbackResponse.Content.ReadAsStringAsync();
                    using var fallbackDoc = JsonDocument.Parse(fallbackJson);
                    root = fallbackDoc.RootElement;

                    if (!root.TryGetProperty("results", out results) || results.GetArrayLength() == 0)
                    {
                        return $"Градът не е намерен";
                    }
                }

                var location = results[0];
                double lat = location.GetProperty("latitude").GetDouble();
                double lon = location.GetProperty("longitude").GetDouble();

                // Взимаме актуалната температура по получените географски координати
                string weatherUrl = $"https://api.open-meteo.com/v1/forecast?latitude={lat}&longitude={lon}&current=temperature_2m,weather_code";
                var weatherResponse = await _httpClient.GetAsync(weatherUrl);

                if (!weatherResponse.IsSuccessStatusCode) return "Времето е недостъпно";

                var weatherJson = await weatherResponse.Content.ReadAsStringAsync();
                using var weatherDoc = JsonDocument.Parse(weatherJson);
                var currentWeather = weatherDoc.RootElement.GetProperty("current");

                double temp = currentWeather.GetProperty("temperature_2m").GetDouble();
                int weatherCode = currentWeather.GetProperty("weather_code").GetInt32();

                string condition = GetWeatherDescription(weatherCode);

                return $"{condition}, {Math.Round(temp)}°C";
            }
            catch
            {
                return "Времето временно недостъпно";
            }
        }

        private string GetWeatherDescription(int code)
        {
            return code switch
            {
                0 => "Ясно и слънчево",
                1 or 2 or 3 => "Облачно",
                45 or 48 => "Мъгла",
                51 or 53 or 55 => "Слаб дъжд",
                61 or 63 => "Дъжд",
                71 or 73 or 75 => "Сняг",
                95 or 96 or 99 => "Буря",
                _ => "Променливо"
            };
        }
    }
}