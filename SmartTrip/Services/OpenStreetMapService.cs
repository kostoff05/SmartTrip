using System.Globalization;
using System.Text.Json;
using Microsoft.Extensions.Caching.Memory;

namespace SmartTrip.Services
{
    public record Restaurant(
        string Name,
        double Latitude,
        double Longitude,
        string MapUrl,
        string Cuisine
    );

    public class OpenStreetMapService
    {
        private readonly HttpClient _httpClient;
        private readonly IMemoryCache _cache;
        private readonly ILogger<OpenStreetMapService> _logger;

        private static readonly SemaphoreSlim NominatimLock = new(1, 1);
        private static DateTime _lastNominatimRequest = DateTime.MinValue;

        private static readonly string[] OverpassServers =
        {
            "https://overpass.kumi.systems/api/interpreter",
            "https://overpass-api.de/api/interpreter"
        };

        public OpenStreetMapService(
            HttpClient httpClient,
            IMemoryCache cache,
            ILogger<OpenStreetMapService> logger)
        {
            _httpClient = httpClient;
            _cache = cache;
            _logger = logger;
        }

        public async Task<List<Restaurant>> GetRestaurantsAsync(string city)
        {
            if (string.IsNullOrWhiteSpace(city))
                return new List<Restaurant>();

            city = city.Trim();

            string cacheKey = $"restaurants:{city.ToLowerInvariant()}";

            if (_cache.TryGetValue(
                cacheKey,
                out List<Restaurant>? cached) && cached != null)
            {
                Console.WriteLine($"OSM CACHE: {city} - {cached.Count} ресторанта");
                return cached;
            }

            try
            {
                // 1. Намираме координатите на града
                string locationUrl =
                    "https://nominatim.openstreetmap.org/search" +
                    $"?q={Uri.EscapeDataString(city)}" +
                    "&format=jsonv2&limit=1";

                string locationJson;

                await NominatimLock.WaitAsync();

                try
                {
                    TimeSpan elapsed =
                        DateTime.UtcNow - _lastNominatimRequest;

                    if (elapsed < TimeSpan.FromSeconds(1.1))
                    {
                        await Task.Delay(
                            TimeSpan.FromSeconds(1.1) - elapsed);
                    }

                    _lastNominatimRequest = DateTime.UtcNow;

                    using var locationResponse =
                        await _httpClient.GetAsync(locationUrl);

                    locationJson =
                        await locationResponse.Content.ReadAsStringAsync();

                    Console.WriteLine("========== NOMINATIM ==========");
                    Console.WriteLine($"Град: {city}");
                    Console.WriteLine($"HTTP статус: {(int)locationResponse.StatusCode}");
                    Console.WriteLine("================================");

                    locationResponse.EnsureSuccessStatusCode();
                }
                finally
                {
                    NominatimLock.Release();
                }

                using var locationDoc = JsonDocument.Parse(locationJson);

                if (locationDoc.RootElement.GetArrayLength() == 0)
                {
                    Console.WriteLine($"NOMINATIM: Няма резултат за {city}");
                    return new List<Restaurant>();
                }

                var location = locationDoc.RootElement[0];

                double lat = double.Parse(
                    location.GetProperty("lat").GetString()!,
                    CultureInfo.InvariantCulture);

                double lon = double.Parse(
                    location.GetProperty("lon").GetString()!,
                    CultureInfo.InvariantCulture);

                string latitude = lat.ToString(CultureInfo.InvariantCulture);
                string longitude = lon.ToString(CultureInfo.InvariantCulture);

                Console.WriteLine($"OSM COORDINATES: {city} -> {latitude}, {longitude}");

                // 2. Създаваме Overpass заявката
                string query = $"""
                    [out:json][timeout:25];
                    (
                      nwr["amenity"="restaurant"]["name"]
                      (around:3000,{latitude},{longitude});
                    );
                    out center;
                    """;

                string? responseBody = null;

                // 3. Опитваме Overpass сървърите последователно
                foreach (string server in OverpassServers)
                {
                    try
                    {
                        Console.WriteLine($"OVERPASS: Опит към {server}");

                        using var content = new FormUrlEncodedContent(
                            new Dictionary<string, string>
                            {
                                ["data"] = query
                            });

                        using var response =
                            await _httpClient.PostAsync(server, content);

                        string body =
                            await response.Content.ReadAsStringAsync();

                        Console.WriteLine("========== OVERPASS ==========");
                        Console.WriteLine($"Сървър: {server}");
                        Console.WriteLine($"HTTP статус: {(int)response.StatusCode}");
                        Console.WriteLine($"Отговор: {body[..Math.Min(200, body.Length)]}");
                        Console.WriteLine("==============================");

                        if (!response.IsSuccessStatusCode)
                            continue;

                        // Проверяваме дали отговорът е валиден JSON
                        using (var testDoc = JsonDocument.Parse(body))
                        {
                            if (!testDoc.RootElement.TryGetProperty(
                                "elements", out _))
                            {
                                Console.WriteLine(
                                    "OVERPASS: Липсва поле elements.");
                                continue;
                            }
                        }

                        responseBody = body;
                        break;
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine(
                            $"OVERPASS ERROR ({server}): {ex.Message}");

                        _logger.LogWarning(
                            ex,
                            "Проблем с Overpass сървър {Server}",
                            server);
                    }
                }

                if (responseBody == null)
                {
                    Console.WriteLine(
                        $"OVERPASS: Всички сървъри се провалиха за {city}");

                    return new List<Restaurant>();
                }

                // 4. Обработваме JSON отговора
                using var doc = JsonDocument.Parse(responseBody);

                var restaurants = new List<Restaurant>();

                foreach (var element in doc.RootElement
                             .GetProperty("elements").EnumerateArray())
                {
                    if (!element.TryGetProperty("tags", out var tags))
                        continue;

                    if (!tags.TryGetProperty("name", out var nameElement))
                        continue;

                    string? name = nameElement.GetString();

                    if (string.IsNullOrWhiteSpace(name))
                        continue;

                    // Пропускаме обекти, маркирани като затворени
                    string[] closedTags =
                    {
                        "disused:amenity",
                        "abandoned:amenity",
                        "demolished:amenity",
                        "disused",
                        "abandoned",
                        "demolished"
                    };

                    bool isClosed = closedTags.Any(tag =>
                    {
                        if (!tags.TryGetProperty(tag, out var value))
                            return false;

                        string? status = value.GetString();

                        return status == "yes" ||
                               status == "restaurant";
                    });

                    if (isClosed)
                        continue;

                    // 5. Координати на ресторанта
                    double? restaurantLat = null;
                    double? restaurantLon = null;

                    if (element.TryGetProperty("lat", out var latElement) &&
                        element.TryGetProperty("lon", out var lonElement))
                    {
                        restaurantLat = latElement.GetDouble();
                        restaurantLon = lonElement.GetDouble();
                    }
                    else if (element.TryGetProperty("center", out var center))
                    {
                        restaurantLat =
                            center.GetProperty("lat").GetDouble();

                        restaurantLon =
                            center.GetProperty("lon").GetDouble();
                    }

                    if (restaurantLat == null || restaurantLon == null)
                        continue;

                    // 6. Тип кухня
                    string cuisine = "Не е посочена";

                    if (tags.TryGetProperty("cuisine", out var cuisineElement))
                    {
                        cuisine =
                            cuisineElement.GetString() ?? "Не е посочена";
                    }

                    string type =
                        element.GetProperty("type").GetString()!;

                    long id =
                        element.GetProperty("id").GetInt64();

                    restaurants.Add(new Restaurant(
                        name,
                        restaurantLat.Value,
                        restaurantLon.Value,
                        $"https://www.openstreetmap.org/{type}/{id}",
                        cuisine
                    ));
                }

                // 7. Премахваме дублираните записи
                var result = restaurants
                    .DistinctBy(r => r.MapUrl)
                    .Take(30)
                    .ToList();

                // 8. Кешираме резултата за 12 часа
                _cache.Set(
                    cacheKey,
                    result,
                    TimeSpan.FromHours(12));

                Console.WriteLine(
                    $"OSM SUCCESS: {city} -> {result.Count} ресторанта");

                _logger.LogInformation(
                    "Намерени {Count} ресторанта за {City}",
                    result.Count,
                    city);

                return result;
            }
            catch (Exception ex)
            {
                Console.WriteLine();
                Console.WriteLine("========== OPENSTREETMAP ERROR ==========");
                Console.WriteLine($"Град: {city}");
                Console.WriteLine($"Тип грешка: {ex.GetType().Name}");
                Console.WriteLine($"Съобщение: {ex.Message}");
                Console.WriteLine(ex);
                Console.WriteLine("=========================================");

                _logger.LogError(
                    ex,
                    "Грешка при зареждане на ресторанти за {City}",
                    city);

                return new List<Restaurant>();
            }
        }
    }
}