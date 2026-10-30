using System.Net.Http.Headers;
using System.Text.Json;

namespace SmartTrip.Services
{
    public class CurrencyService
    {
        private readonly HttpClient _httpClient;
        private readonly ILogger<CurrencyService> _logger;
        private readonly IConfiguration _configuration;

        public CurrencyService(
            HttpClient httpClient,
            ILogger<CurrencyService> logger,
            IConfiguration configuration)
        {
            _httpClient = httpClient;
            _logger = logger;
            _configuration = configuration;

            _httpClient.Timeout = TimeSpan.FromSeconds(15);
        }

        public async Task<DestinationCurrency?> GetCurrencyAsync(string city)
        {
            if (string.IsNullOrWhiteSpace(city))
                return null;

            try
            {
                // 1. Намираме държавата чрез Nominatim

                string geocodingUrl =
                    "https://nominatim.openstreetmap.org/search" +
                    $"?q={Uri.EscapeDataString(city.Trim())}" +
                    "&format=jsonv2&addressdetails=1&limit=1&accept-language=bg,en";

                using var geoResponse =
                    await _httpClient.GetAsync(geocodingUrl);

                string geoJson =
                    await geoResponse.Content.ReadAsStringAsync();

                if (!geoResponse.IsSuccessStatusCode)
                {
                    _logger.LogWarning(
                        "Nominatim грешка: {Response}", geoJson);

                    return null;
                }

                using var geoDocument = JsonDocument.Parse(geoJson);

                var results = geoDocument.RootElement;

                if (results.ValueKind != JsonValueKind.Array ||
                    results.GetArrayLength() == 0)
                {
                    _logger.LogWarning(
                        "Не е намерен град: {City}", city);

                    return null;
                }

                var location = results[0];

                if (!location.TryGetProperty("address", out var address))
                    return null;

                if (!address.TryGetProperty(
                    "country_code", out var codeElement))
                    return null;

                string countryCode =
                    codeElement.GetString()?.ToUpperInvariant() ?? "";

                if (string.IsNullOrWhiteSpace(countryCode))
                    return null;

                // 2. Вземаме API ключа

                string? apiKey =
                    _configuration["RestCountries:ApiKey"];

                if (string.IsNullOrWhiteSpace(apiKey))
                {
                    _logger.LogWarning("Липсва REST Countries API ключ.");
                    return null;
                }

                // 3. Изпращаме заявка към REST Countries v5

                string currencyUrl =
                    $"https://api.restcountries.com/countries/v5/codes.alpha_2/{countryCode}" +
                    "?response_fields=names.common,currencies";

                using var request = new HttpRequestMessage(
                    HttpMethod.Get, currencyUrl);

                request.Headers.Authorization =
                    new AuthenticationHeaderValue("Bearer", apiKey);

                using var currencyResponse =
                    await _httpClient.SendAsync(request);

                string currencyJson =
                    await currencyResponse.Content.ReadAsStringAsync();

                if (!currencyResponse.IsSuccessStatusCode)
                {
                    _logger.LogWarning(
                        "REST Countries грешка: {Status}, {Response}",
                        currencyResponse.StatusCode, currencyJson);

                    return null;
                }

                // 4. Прочитаме отговора

                using var currencyDocument =
                    JsonDocument.Parse(currencyJson);

                var root = currencyDocument.RootElement;

                if (!root.TryGetProperty("data", out var data) ||
                    !data.TryGetProperty("objects", out var objects) ||
                    objects.ValueKind != JsonValueKind.Array ||
                    objects.GetArrayLength() == 0)
                    return null;

                var country = objects[0];

                string countryName = country
                    .GetProperty("names")
                    .GetProperty("common")
                    .GetString() ?? "";

                // 5. Извличаме валутата

                if (!country.TryGetProperty(
                    "currencies", out var currencies) ||
                    currencies.ValueKind != JsonValueKind.Array ||
                    currencies.GetArrayLength() == 0)
                    return null;

                var currency = currencies[0];

                string code =
                    currency.GetProperty("code").GetString() ?? "";

                string name =
                    currency.GetProperty("name").GetString() ?? code;

                string symbol = "";

                if (currency.TryGetProperty("symbol", out var symbolElement))
                    symbol = symbolElement.GetString() ?? "";

                return new DestinationCurrency(
                    countryName, name, code, symbol);
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Грешка при извличане на валута за {City}",
                    city);

                return null;
            }
        }
    }

    public class DestinationCurrency
    {
        public string CountryName { get; set; }
        public string CurrencyName { get; set; }
        public string CurrencyCode { get; set; }
        public string Symbol { get; set; }

        public DestinationCurrency(
            string countryName,
            string currencyName,
            string currencyCode,
            string symbol)
        {
            CountryName = countryName;
            CurrencyName = currencyName;
            CurrencyCode = currencyCode;
            Symbol = symbol;
        }
    }
}
