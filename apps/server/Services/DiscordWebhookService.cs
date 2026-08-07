using System.Text;
using System.Text.Json;

namespace Bordle.Server.Services
{
    public class DiscordWebhookService(IHttpClientFactory httpClientFactory, ILogger<DiscordWebhookService> logger)
    {
        // max message content length for Discord webhooks is 2000 characters
        // https://docs.discord.com/developers/resources/webhook#execute-webhook
        private readonly int MaxWebhookMessageLength = 2000;
        // only include the first n players in the streak notification if the message is too long
        private readonly int NumOldestPlayers = 3;

        public async Task SendStreakNotificationAsync(Data.AppDbContext db, long guildId, string webhookUrl, int streakCount, IEnumerable<string?> playerUsernames)
        {
            var validPlayers = playerUsernames.Where(u => !string.IsNullOrEmpty(u)).ToList();

            var playerList = validPlayers.Any()
                ? string.Join(", ", validPlayers)
                : "no one";

            var messagePrefix = $"🔥 Your server is on a **{streakCount}** day streak! Here are the players from yesterday: ";

            if (messagePrefix.Length + playerList.Length > MaxWebhookMessageLength)
            {
                var topPlayers = validPlayers.Take(NumOldestPlayers);
                playerList = $"{string.Join(", ", topPlayers)} and {validPlayers.Count - NumOldestPlayers} others";
            }

            var message = $"{messagePrefix}{playerList}";

            await PostToWebhookAsync(db, guildId, webhookUrl, message);
        }

        public async Task<bool> SendTestPingAsync(string webhookUrl)
        {
            try
            {
                var client = httpClientFactory.CreateClient("Discord");
                var response = await client.GetAsync(webhookUrl);
                return response.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Exception while sending test ping to Discord webhook.");
                return false;
            }
        }

        private async Task PostToWebhookAsync(Data.AppDbContext db, long guildId, string webhookUrl, string content)
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

                    if (response.StatusCode == System.Net.HttpStatusCode.NotFound ||
                        response.StatusCode == System.Net.HttpStatusCode.Unauthorized ||
                        response.StatusCode == System.Net.HttpStatusCode.Forbidden)
                    {
                        var guild = await db.Guilds.FindAsync(guildId);
                        if (guild != null)
                        {
                            guild.IsSubscribed = false;
                            await db.SaveChangesAsync();
                            logger.LogInformation("Automatically unsubscribed guild {GuildId} due to dead webhook.", guildId);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Exception while posting to Discord webhook.");
            }
        }
    }
}
