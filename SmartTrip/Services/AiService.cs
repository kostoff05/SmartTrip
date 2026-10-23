using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace SmartTrip.Services
{
    public class AiService
    {
        private readonly HttpClient _httpClient;
        private readonly string? _apiKey;

        public AiService(HttpClient httpClient, IConfiguration configuration)
        {
            _httpClient = httpClient;
            _apiKey = configuration["GeminiSettings:ApiKey"];
        }

        public async Task<string> GenerateTravelTipAsync(string city, string travelTypes, string budget)
        {
            if (string.IsNullOrEmpty(city)) return "Няма достатъчно данни за AI анализ.";

            if (string.IsNullOrEmpty(_apiKey))
            {
                return "ГРЕШКА: Ключът липсва в appsettings.json!";
            }

            try
            {
                // За AQ... ключове ползваме стандартния URL без ?key= накрая
                string url = "https://generativelanguage.googleapis.com/v1beta/models/gemini-1.5-flash:generateContent";

                // Подаваме ключа като Bearer token в хедъра на заявката
                _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _apiKey);

                var requestBody = new
                {
                    contents = new[]
                    {
                        new
                        {
                            parts = new[]
                            {
                                new { text = $"Напиши подробен туристически пътеводител за град {city} на български език. Бюджет: {budget} лв., интереси: {travelTypes}. Използвай HTML тагове (като <h4>, <ul>, <li>, <strong>) за следните секции: 1. Летища и Трансфери с цени, 2. Топ Забележителности, 3. Скрити Места, 4. Ресторанти." }
                            }
                        }
                    }
                };

                string jsonContent = JsonSerializer.Serialize(requestBody);
                var content = new StringContent(jsonContent, Encoding.UTF8, "application/json");

                var response = await _httpClient.PostAsync(url, content);

                if (!response.IsSuccessStatusCode)
                {
                    string errorDetails = await response.Content.ReadAsStringAsync();
                    return $"Грешка от Google API ({response.StatusCode}): {errorDetails}";
                }

                string responseString = await response.Content.ReadAsStringAsync();
                using var doc = JsonDocument.Parse(responseString);

                var text = doc.RootElement
                    .GetProperty("candidates")[0]
                    .GetProperty("content")
                    .GetProperty("parts")[0]
                    .GetProperty("text")
                    .GetString();

                return text?.Trim() ?? "Няма генериран отговор.";
            }
            catch (Exception ex)
            {
                return $"Възникна грешка при връзка с AI асистента: {ex.Message}";
            }
        }
    }
}