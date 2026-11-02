using System;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.Json;

namespace SmartTrip.Services
{
    public class WeatherService
    {
        private readonly HttpClient _httpClient;
        private readonly ILogger<WeatherService> _logger;

        public WeatherService(
            HttpClient httpClient,
            ILogger<WeatherService> logger)
        {
            _httpClient = httpClient;
            _logger = logger;

            _httpClient.Timeout = TimeSpan.FromSeconds(15);
        }
        // 1. ТЕКУЩО ВРЕМЕ
        // Запазваме метода за съществуващите страници
        public async Task<string> GetWeatherAsync(string city)
        {
            if (string.IsNullOrWhiteSpace(city))
            {
                return "Моля, въведете град.";
            }

            try
            {
                var coordinates = await GetCoordinatesAsync(city);

                if (coordinates == null)
                {
                    return $"Градът „{city}“ не е намерен.";
                }

                string latitude = coordinates.Value.Latitude
                    .ToString(CultureInfo.InvariantCulture);

                string longitude = coordinates.Value.Longitude
                    .ToString(CultureInfo.InvariantCulture);

                string weatherUrl =
                    "https://api.open-meteo.com/v1/forecast" +
                    $"?latitude={latitude}" +
                    $"&longitude={longitude}" +
                    "&current=temperature_2m,weather_code" +
                    "&timezone=auto";

                using var response =
                    await _httpClient.GetAsync(weatherUrl);

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogWarning(
                        "Open-Meteo Current Weather API върна HTTP {StatusCode} за град {City}",
                        (int)response.StatusCode,
                        city);

                    return "В момента няма достъп до информацията за времето.";
                }

                string json =
                    await response.Content.ReadAsStringAsync();

                using var document = JsonDocument.Parse(json);

                if (!document.RootElement.TryGetProperty(
                    "current", out var current))
                {
                    return "Не получихме данни за текущото време.";
                }

                double temperature = current
                    .GetProperty("temperature_2m")
                    .GetDouble();

                int weatherCode = current
                    .GetProperty("weather_code")
                    .GetInt32();

                string description =
                    GetWeatherDescription(weatherCode);

                return $"{description}, {Math.Round(temperature)}°C";
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Грешка при зареждане на текущото време за {City}",
                    city);

                return "Възникна грешка при зареждането на времето.";
            }
        }
        // 2. ПРОГНОЗА ЗА ПЕРИОДА НА ПЪТУВАНЕТО
        public async Task<string> GetTripWeatherAsync(
            string city,
            DateTime startDate,
            DateTime endDate)
        {
            if (string.IsNullOrWhiteSpace(city))
            {
                return "Не е посочена дестинация.";
            }

            if (endDate.Date < startDate.Date)
            {
                return "Крайната дата не може да бъде преди началната.";
            }

            try
            {
                DateTime today = DateTime.UtcNow.Date;

                DateTime forecastStart = startDate.Date;
                DateTime forecastEnd = endDate.Date;

                // Open-Meteo предоставя прогноза за ограничен
                // период напред. Използваме до 16 дни,
                // включително днешния ден.

                DateTime lastForecastDate = today.AddDays(15);

                // Ако цялото пътуване е извън прогнозния период
                if (forecastStart > lastForecastDate)
                {
                    return GetSeasonalWeatherAdvice(
                        city,
                        startDate,
                        endDate);
                }

                // Ако пътуването е изцяло в миналото
                if (forecastEnd < today)
                {
                    return "Периодът на пътуването е в миналото. " +
                           "Не е налична актуална прогноза за тези дати.";
                }

                var coordinates = await GetCoordinatesAsync(city);

                if (coordinates == null)
                {
                    return $"Не успяхме да намерим координати за {city}.";
                }

                string latitude = coordinates.Value.Latitude
                    .ToString(CultureInfo.InvariantCulture);

                string longitude = coordinates.Value.Longitude
                    .ToString(CultureInfo.InvariantCulture);

                // Ограничаваме заявката само до дните,
                // за които има налична прогноза.

                DateTime requestStart =
                    forecastStart < today ? today : forecastStart;

                DateTime requestEnd =
                    forecastEnd > lastForecastDate
                        ? lastForecastDate
                        : forecastEnd;

                string start = requestStart.ToString("yyyy-MM-dd");
                string end = requestEnd.ToString("yyyy-MM-dd");

                string weatherUrl =
                    "https://api.open-meteo.com/v1/forecast" +
                    $"?latitude={latitude}" +
                    $"&longitude={longitude}" +
                    "&daily=temperature_2m_max,temperature_2m_min," +
                    "precipitation_sum,weather_code" +
                    $"&start_date={start}" +
                    $"&end_date={end}" +
                    "&timezone=auto";

                using var response =
                    await _httpClient.GetAsync(weatherUrl);

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogWarning(
                        "Open-Meteo върна HTTP {StatusCode} за {City}",
                        (int)response.StatusCode,
                        city);

                    return "Прогнозата за избраните дати не е достъпна. " +
                           GetSeasonalWeatherAdvice(
                               city,
                               startDate,
                               endDate);
                }

                string json =
                    await response.Content.ReadAsStringAsync();

                using var document = JsonDocument.Parse(json);

                if (!document.RootElement.TryGetProperty(
                    "daily", out var daily))
                {
                    return "Няма налична дневна прогноза. " +
                           GetSeasonalWeatherAdvice(
                               city,
                               startDate,
                               endDate);
                }

                var dates = daily.GetProperty("time");
                var maxTemperatures =
                    daily.GetProperty("temperature_2m_max");

                var minTemperatures =
                    daily.GetProperty("temperature_2m_min");

                var precipitation =
                    daily.GetProperty("precipitation_sum");

                var weatherCodes =
                    daily.GetProperty("weather_code");

                var result = new StringBuilder();

                result.AppendLine(
                    $"ПРОГНОЗА ЗА ВРЕМЕТО В {city.ToUpperInvariant()}");

                result.AppendLine(
                    $"Период: {startDate:dd.MM.yyyy} - {endDate:dd.MM.yyyy}");

                result.AppendLine();

                double lowestTemperature = double.MaxValue;
                double highestTemperature = double.MinValue;

                bool hasRain = false;
                bool hasSnow = false;

                for (int i = 0; i < dates.GetArrayLength(); i++)
                {
                    string date = dates[i].GetString() ?? "";

                    double maxTemp =
                        maxTemperatures[i].GetDouble();

                    double minTemp =
                        minTemperatures[i].GetDouble();

                    double rain =
                        precipitation[i].GetDouble();

                    int code =
                        weatherCodes[i].GetInt32();

                    lowestTemperature =
                        Math.Min(lowestTemperature, minTemp);

                    highestTemperature =
                        Math.Max(highestTemperature, maxTemp);

                    if (rain > 0 || IsRainyWeather(code))
                    {
                        hasRain = true;
                    }

                    if (IsSnowyWeather(code))
                    {
                        hasSnow = true;
                    }

                    string description =
                        GetWeatherDescription(code);

                    result.AppendLine(
                        $"{date}: {description}, " +
                        $"от {Math.Round(minTemp)}°C " +
                        $"до {Math.Round(maxTemp)}°C, " +
                        $"валежи: {rain:0.#} мм");
                }

                result.AppendLine();

                result.AppendLine(
                    $"Температурен диапазон: " +
                    $"{Math.Round(lowestTemperature)}°C - " +
                    $"{Math.Round(highestTemperature)}°C");
                // 3. ПРЕПОРЪКИ ЗА ОБЛЕКЛО
                result.AppendLine();
                result.AppendLine("ПРЕПОРЪКИ ЗА ОБЛЕКЛО:");

                if (highestTemperature <= 5)
                {
                    result.AppendLine(
                        "- Дебело зимно яке, пуловери и термобельо.");

                    result.AppendLine(
                        "- Топли обувки, шапка, шал и ръкавици.");
                }
                else if (highestTemperature <= 15)
                {
                    result.AppendLine(
                        "- Топло яке или палто.");

                    result.AppendLine(
                        "- Блузи с дълъг ръкав, пуловер и дълги панталони.");

                    result.AppendLine(
                        "- Удобни затворени обувки.");
                }
                else if (highestTemperature <= 23)
                {
                    result.AppendLine(
                        "- Леко яке или суитшърт за сутрините и вечерите.");

                    result.AppendLine(
                        "- Тениски, дълги панталони и удобни обувки.");
                }
                else
                {
                    result.AppendLine(
                        "- Леки летни дрехи, тениски и къси панталони.");

                    result.AppendLine(
                        "- Слънчеви очила, шапка и слънцезащитен крем.");
                }

                if (hasRain)
                {
                    result.AppendLine(
                        "- Вземете чадър и водоустойчиво яке.");
                }

                if (hasSnow)
                {
                    result.AppendLine(
                        "- Подгответе зимни обувки с добро сцепление.");
                }

                result.AppendLine(
                    "- Изберете удобни обувки за продължително ходене.");

                result.AppendLine();

                result.AppendLine(
                    "ВАЖНО: Това е метеорологична прогноза, " +
                    "която може да се промени.");

                // Ако прогнозата покрива само част от пътуването
                if (forecastEnd > lastForecastDate)
                {
                    result.AppendLine();

                    result.AppendLine(
                        "За останалите дни все още няма надеждна прогноза.");

                    result.AppendLine(
                        GetSeasonalWeatherAdvice(
                            city,
                            lastForecastDate.AddDays(1),
                            endDate));
                }

                return result.ToString();
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Грешка при прогнозата за {City}",
                    city);

                return "Не успяхме да извлечем прогнозата. " +
                       GetSeasonalWeatherAdvice(
                           city,
                           startDate,
                           endDate);
            }
        }
        // 4. НАМИРАНЕ НА КООРДИНАТИ
        private async Task<(double Latitude, double Longitude)?>
            GetCoordinatesAsync(string city)
        {
            string trimmedCity = city.Trim();

            string[] urls =
            {
                "https://geocoding-api.open-meteo.com/v1/search" +
                $"?name={Uri.EscapeDataString(trimmedCity)}" +
                "&count=10&language=bg&format=json",

                "https://geocoding-api.open-meteo.com/v1/search" +
                $"?name={Uri.EscapeDataString(trimmedCity)}" +
                "&count=10&format=json"
            };

            foreach (string url in urls)
            {
                using var response =
                    await _httpClient.GetAsync(url);

                if (!response.IsSuccessStatusCode)
                {
                    continue;
                }

                string json =
                    await response.Content.ReadAsStringAsync();

                var coordinates = ParseCoordinates(json);

                if (coordinates != null)
                {
                    return coordinates;
                }
            }

            return null;
        }
        // 5. ОБРАБОТКА НА КООРДИНАТИТЕ
        private static (double Latitude, double Longitude)?
            ParseCoordinates(string json)
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

            var location = results[0];

            if (!location.TryGetProperty(
                "latitude", out var latitude) ||
                !location.TryGetProperty(
                    "longitude", out var longitude))
            {
                return null;
            }

            return (
                latitude.GetDouble(),
                longitude.GetDouble()
            );
        }
        // 6. СЕЗОННИ ПРЕПОРЪКИ
        private static string GetSeasonalWeatherAdvice(
            string city,
            DateTime startDate,
            DateTime endDate)
        {
            int month = startDate.Month;

            string season;

            if (month == 12 || month == 1 || month == 2)
            {
                season = "зима";
            }
            else if (month >= 3 && month <= 5)
            {
                season = "пролет";
            }
            else if (month >= 6 && month <= 8)
            {
                season = "лято";
            }
            else
            {
                season = "есен";
            }

            return
                $"За {city} през периода " +
                $"{startDate:dd.MM.yyyy} - {endDate:dd.MM.yyyy} " +
                $"няма налична точна прогноза. " +
                $"Според календарния сезон ({season}) " +
                "подгответе подходящи дрехи, " +
                "но проверете реалната прогноза " +
                "няколко дни преди отпътуването. " +
                "Важно: сезонът зависи от полукълбото " +
                "и местния климат на дестинацията.";
        }
        // 7. ПОМОЩНИ МЕТОДИ
        private static bool IsRainyWeather(int code)
        {
            return code is
                51 or 53 or 55 or
                56 or 57 or
                61 or 63 or 65 or
                66 or 67 or
                80 or 81 or 82 or
                95 or 96 or 99;
        }

        private static bool IsSnowyWeather(int code)
        {
            return code is
                71 or 73 or 75 or 77 or 85 or 86;
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