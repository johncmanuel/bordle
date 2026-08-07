using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Bordle.Server.Services
{
   public static class DiscordCommandRegistrar
    {
        private static readonly string _discordApiBase = "https://discord.com/api/v10";

        public static async Task<bool> TryExecuteAsync(string[] args)
        {
            if (!args.Contains("--register-commands"))
                return false;

            var builder = WebApplication.CreateBuilder(args);
            DotNetEnv.Env.TraversePath().Load();
            builder.Configuration.AddEnvironmentVariables();
            await using var app = builder.Build();

            var logger = app.Services.GetRequiredService<ILogger<Program>>();
            await RegisterCommandsAsync(app.Services.GetRequiredService<IConfiguration>(), logger);

            return true;
        }

        private static async Task RegisterCommandsAsync(IConfiguration config, ILogger logger)
        {
            var appId = config["VITE_DISCORD_CLIENT_ID"]
                ?? throw new InvalidOperationException("VITE_DISCORD_CLIENT_ID is not configured.");
            var clientSecret = config["CLIENT_SECRET"]
                ?? throw new InvalidOperationException("CLIENT_SECRET is not configured.");

            using var client = new HttpClient();

            logger.LogInformation("Fetching OAuth2 bearer token via client_credentials...");

            var tokenRequest = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["grant_type"] = "client_credentials",
                ["scope"] = "applications.commands.update",
                ["client_id"] = appId,
                ["client_secret"] = clientSecret,
            });

            var tokenResponse = await client.PostAsync($"{_discordApiBase}/oauth2/token", tokenRequest);
            var tokenBody = await tokenResponse.Content.ReadAsStringAsync();

            if (!tokenResponse.IsSuccessStatusCode)
            {
                logger.LogError("Failed to fetch OAuth2 token. Status: {Status}. Body: {Body}",
                    tokenResponse.StatusCode, tokenBody);
                return;
            }

            var tokenResult = JsonSerializer.Deserialize<OAuthTokenResponse>(tokenBody)
                ?? throw new InvalidOperationException("Failed to parse OAuth2 token response.");

            var subscribeOption = new CommandOption("webhook_url", "The Discord webhook URL to send puzzle notifications to.", 3, true);

            var commands = new[]
            {
                new CommandBody("subscribe", "Subscribe this server to Bordle puzzle notifications via a webhook.", [subscribeOption]),
                new CommandBody("unsubscribe", "Unsubscribe this server from Bordle puzzle notifications.", [])
            };

            client.DefaultRequestHeaders.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", tokenResult.AccessToken);

            var url = $"{_discordApiBase}/applications/{appId}/commands";

            logger.LogInformation("Registering {Count} global slash command(s) with Discord individually...", commands.Length);

            bool allSuccess = true;
            foreach (var command in commands)
            {
                var content = new StringContent(JsonSerializer.Serialize(command, new JsonSerializerOptions
                {
                    PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
                }), Encoding.UTF8, "application/json");

                var response = await client.PostAsync(url, content);
                var responseBody = await response.Content.ReadAsStringAsync();

                if (response.IsSuccessStatusCode)
                {
                    logger.LogInformation("✅ Successfully registered command: /{Name}", command.Name);
                }
                else
                {
                    allSuccess = false;
                    logger.LogError("❌ Failed to register command: /{Name}. Status: {Status}. Body: {Body}",
                        command.Name, response.StatusCode, responseBody);
                }
            }

            if (allSuccess)
            {
                logger.LogInformation("All commands registered successfully.");
            }
        }
    }
}

// Record types used for JSON serialization of Discord slash command payloads
internal sealed record CommandOption(string Name, string Description, int Type, bool Required);
internal sealed record CommandBody(string Name, string Description, CommandOption[] Options);
internal sealed record OAuthTokenResponse([property: JsonPropertyName("access_token")] string AccessToken);

