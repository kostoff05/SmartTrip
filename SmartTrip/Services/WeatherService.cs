
using System.Text.Json;

namespace SmartTrip.Services
{
    public class WeatherService
    {
        private readonly HttpClient _httpClient;

        public WeatherService(HttpClient httpClient)
        {
            _httpClient = httpClient;
            _httpClient.Timeout = TimeSpan.FromSeconds(15);
        }

        public async Task<string> GetWeatherAsync(string city)
        {
            if (string.IsNullOrWhiteSpace(city))
            {
                return "Моля, въведете град.";
            }

            try
            {
                string trimmedCity = city.Trim();

                // 1. Търсим града по име.
                string geoUrl =
                    $"https://geocoding-api.open-meteo.com/v1/search" +
                    $"?name={Uri.EscapeDataString(trimmedCity)}" +
                    "&count=10&language=bg&format=json";

                var geoResponse = await _httpClient.GetAsync(geoUrl);

                if (!geoResponse.IsSuccessStatusCode)
                {
                    return "Услугата за търсене на градове не отговаря.";
                }

                string geoJson = await geoResponse.Content.ReadAsStringAsync();

                var coordinates = ParseCoordinates(geoJson);

                // 2. Ако не намерим града, опитваме без езиков параметър.
                if (coordinates == null)
                {
                    string fallbackUrl =
                        $"https://geocoding-api.open-meteo.com/v1/search" +
                        $"?name={Uri.EscapeDataString(trimmedCity)}" +
                        "&count=10&format=json";

                    var fallbackResponse =
                        await _httpClient.GetAsync(fallbackUrl);

                    if (!fallbackResponse.IsSuccessStatusCode)
                    {
                        return "Не успяхме да намерим града.";
                    }

                    string fallbackJson =
                        await fallbackResponse.Content.ReadAsStringAsync();

                    coordinates = ParseCoordinates(fallbackJson);
                }

                if (coordinates == null)
                {
                    return $"Градът „{trimmedCity}“ не е намерен.";
                }

                double latitude = coordinates.Value.Latitude;
                double longitude = coordinates.Value.Longitude;

                // 3. Извличаме актуалното време по координатите.
                string weatherUrl =
                    "https://api.open-meteo.com/v1/forecast" +
                    $"?latitude={latitude.ToString(System.Globalization.CultureInfo.InvariantCulture)}" +
                    $"&longitude={longitude.ToString(System.Globalization.CultureInfo.InvariantCulture)}" +
                    "&current=temperature_2m,weather_code" +
                    "&timezone=auto";

                var weatherResponse = await _httpClient.GetAsync(weatherUrl);

                if (!weatherResponse.IsSuccessStatusCode)
                {
                    return "В момента няма достъп до информацията за времето.";
                }

                string weatherJson =
                    await weatherResponse.Content.ReadAsStringAsync();

                using var weatherDocument = JsonDocument.Parse(weatherJson);

                if (!weatherDocument.RootElement.TryGetProperty(
                    "current", out var current))
                {
                    return "Не получихме данни за текущото време.";
                }

                double temperature =
                    current.GetProperty("temperature_2m").GetDouble();

                int weatherCode =
                    current.GetProperty("weather_code").GetInt32();

                string description = GetWeatherDescription(weatherCode);

                return $"{description}, {Math.Round(temperature)}°C";
            }
            catch (TaskCanceledException)
            {
                return "Заявката за времето отне твърде много време.";
            }
            catch (HttpRequestException)
            {
                return "Няма връзка с услугата за времето.";
            }
            catch (JsonException)
            {
                return "Получените данни за времето са невалидни.";
            }
            catch (Exception)
            {
                return "Възникна грешка при зареждането на времето.";
            }
        }

        private static (double Latitude, double Longitude)? ParseCoordinates(
            string json)
        {
            using var document = JsonDocument.Parse(json);

            if (!document.RootElement.TryGetProperty(
                "results", out var results))
            {
                return null;
            }

            if (results.ValueKind != JsonValueKind.Array ||
                results.GetArrayLength() == 0)
            {
                return null;
            }

            // Взимаме координатите, докато JSON документът е отворен.
            var location = results[0];

            if (!location.TryGetProperty("latitude", out var latitude) ||
                !location.TryGetProperty("longitude", out var longitude))
            {
                return null;
            }

            return (
                latitude.GetDouble(),
                longitude.GetDouble()
            );
        }

        private static string GetWeatherDescription(int code)
        {
            return code switch
            {
                0 => "Ясно и слънчево",
                1 => "Предимно ясно",
                2 => "Променлива облачност",
                3 => "Облачно",
                45 or 48 => "Мъгла",
                51 or 53 or 55 => "Ръмеж",
                56 or 57 => "Леден ръмеж",
                61 => "Слаб дъжд",
                63 => "Умерен дъжд",
                65 => "Силен дъжд",
                66 or 67 => "Леден дъжд",
                71 => "Слаб сняг",
                73 => "Умерен сняг",
                75 => "Силен сняг",
                77 => "Снежни зърна",
                80 => "Слаби превалявания",
                81 => "Умерени превалявания",
                82 => "Силни превалявания",
                85 or 86 => "Снежни превалявания",
                95 => "Гръмотевична буря",
                96 or 99 => "Буря с градушка",
                _ => "Променливо време"
            };
        }
    }
}
