using System.Text;
using System.Text.Json;

namespace Bordle.Server.Services
{
    public class DiscordWebhookService(IHttpClientFactory httpClientFactory, ILogger<DiscordWebhookService> logger)
    {
        public async Task SendStreakNotificationAsync(string webhookUrl, int streakCount, IEnumerable<string?> playerUsernames)
        {
            var playerList = playerUsernames.Any()
                ? string.Join(", ", playerUsernames.Where(u => !string.IsNullOrEmpty(u)))
                : "no one";

            var message = $"🔥 Your server is on a **{streakCount}** day streak! Here are the players from yesterday: {playerList}";

            await PostToWebhookAsync(webhookUrl, message);
        }

        private async Task PostToWebhookAsync(string webhookUrl, string content)
        {
            try
            {
                var client = httpClientFactory.CreateClient("Discord");
                var payload = JsonSerializer.Serialize(new { content });
                var requestContent = new StringContent(payload, Encoding.UTF8, "application/json");

                var response = await client.PostAsync(webhookUrl, requestContent);

                if (!response.IsSuccessStatusCode)
                {
                    var body = await response.Content.ReadAsStringAsync();
                    logger.LogWarning("Discord webhook POST failed. Status: {Status}. Body: {Body}", response.StatusCode, body);
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Exception while posting to Discord webhook: {Url}", webhookUrl);
            }
        }
    }
}
