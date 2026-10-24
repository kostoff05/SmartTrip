
using System.Text;
using System.Text.Json;

namespace SmartTrip.Services
{
    public class AiService
    {
        private readonly HttpClient _httpClient;
        private readonly string? _apiKey;

        public AiService(
            HttpClient httpClient,
            IConfiguration configuration)
        {
            _httpClient = httpClient;
            _apiKey = configuration["GeminiSettings:ApiKey"]?.Trim();
        }

        public async Task<string> GenerateTravelTipAsync(
            string city,
            string travelTypes,
            string budget)
        {
            if (string.IsNullOrWhiteSpace(city))
                return "Няма достатъчно данни за AI анализ.";

            if (string.IsNullOrWhiteSpace(_apiKey))
                return "ГРЕШКА: Липсва Gemini API ключ!";

            try
            {
                string url =
    "https://generativelanguage.googleapis.com/v1beta/models/gemini-3.5-flash-lite:generateContent";

                var requestBody = new
                {
                    contents = new[]
                    {
                        new
                        {
                            parts = new[]
                            {
                                new
                                {
                                    text = $"""
                                    Напиши подробен туристически
                                    пътеводител за град {city}
                                    на български език.

                                    Бюджет: {budget} €.

                                    ВАЖНО:
                                    - Използвай само евро (EUR, €) за всички цени.
                                    - България използва еврото от 1 януари 2026 г.
                                    - Не използвай български левове (BGN, лв.).
                                    - Всички цени за хотели, ресторанти, транспорт
                                      и забележителности трябва да бъдат в евро.
                                    - Не измисляй точни цени, ако нямаш актуални данни.

                                    Интереси: {travelTypes}.

                                    Използвай HTML тагове:
                                    <h4>, <ul>, <li>, <strong>.

                                    Включи следните секции:
                                    1. Летища и трансфери с цени
                                    2. Топ забележителности
                                    3. Скрити места
                                    4. Ресторанти
                                    """
                                }
                            }
                        }
                    }
                };

                string jsonContent =
                    JsonSerializer.Serialize(requestBody);

                using var request = new HttpRequestMessage(
                    HttpMethod.Post, url);

                request.Headers.Add(
                    "X-goog-api-key", _apiKey);

                request.Content = new StringContent(
                    jsonContent,
                    Encoding.UTF8,
                    "application/json");

                using var response =
                    await _httpClient.SendAsync(request);

                string responseString =
                    await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                {
                    return $"Грешка от Gemini API " +
                           $"({response.StatusCode}): " +
                           responseString;
                }

                using var doc =
                    JsonDocument.Parse(responseString);

                var text = doc.RootElement
                    .GetProperty("candidates")[0]
                    .GetProperty("content")
                    .GetProperty("parts")[0]
                    .GetProperty("text")
                    .GetString();

                return text?.Trim()
                    ?? "Няма генериран отговор.";
            }
            catch (Exception ex)
            {
                return "Възникна грешка при връзка " +
                       "с AI асистента: " + ex.Message;
            }
        }
    }
}
