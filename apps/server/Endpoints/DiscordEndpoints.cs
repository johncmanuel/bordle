using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using NSec.Cryptography;
using Bordle.Server.Data;
using Bordle.Server.Data.Models;
using Bordle.Server.Services;

// solid resource for organizing minimal APIs in .NET: https://www.tessferrandez.com/blog/2023/10/31/organizing-minimal-apis.html

public static class DiscordEndpoints
{
    private static readonly string _discordApiBaseUrl = "https://discord.com/api";

    public static void RegisterDiscordEndpoints(this WebApplication app)
    {
        var discord = app.MapGroup("/api/discord").WithTags("Discord endpoints");

        discord.MapPost("/token", GetDiscordToken);
        discord.MapPost("/interactions", HandleInteraction).AllowAnonymous();
    }

    private static async Task<Results<Ok<TokenResponse>, BadRequest<string>>> GetDiscordToken(
        TokenRequest req,
        AppDbContext db,
        JwtService jwtService,
        IHttpClientFactory httpClientFactory,
        IConfiguration config)
    {
        var _httpClient = httpClientFactory.CreateClient("Discord");
        string accessToken;

#if DEBUG
        // If the client is using DiscordSDKMock, it sends a "mock_code". Return a mock token response in that case.
        if (req.Code == "mock_code")
        {
            // add mock user and guild 
            const long mockUserId = 1L;
            const long mockGuildId = 1L;

            await UpsertUserAndGuild(db, mockUserId, "BordleDev", null, mockGuildId);

            var mockSessionToken = jwtService.GenerateToken(mockUserId, mockGuildId);
            return TypedResults.Ok(new TokenResponse("mock_token", mockSessionToken));
        }
#endif

        if (string.IsNullOrEmpty(req.Code)) return TypedResults.BadRequest("Code is missing from request.");
        if (string.IsNullOrEmpty(req.GuildId)) return TypedResults.BadRequest("GuildId is missing from request. Ensure JSON property names match.");

        var clientId = config["VITE_DISCORD_CLIENT_ID"];
        var clientSecret = config["CLIENT_SECRET"];

        if (string.IsNullOrEmpty(clientId)) return TypedResults.BadRequest("VITE_DISCORD_CLIENT_ID configuration is missing on server.");
        if (string.IsNullOrEmpty(clientSecret)) return TypedResults.BadRequest("CLIENT_SECRET configuration is missing on server.");

        // Exchange the OAuth code for an access token with Discord
        var content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["client_id"] = clientId,
            ["client_secret"] = clientSecret,
            ["grant_type"] = "authorization_code",
            ["code"] = req.Code
        });

        var tokenResponse = await _httpClient.PostAsync($"{_discordApiBaseUrl}/oauth2/token", content);

        if (!tokenResponse.IsSuccessStatusCode)
        {
            var err = await tokenResponse.Content.ReadAsStringAsync();
            return TypedResults.BadRequest($"Failed to retrieve token from Discord. Status: {tokenResponse.StatusCode}. Error: {err}");
        }

        var tokenResult = await tokenResponse.Content.ReadFromJsonAsync<DiscordTokenResult>(
            new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower }
        );
        if (tokenResult == null || string.IsNullOrEmpty(tokenResult.AccessToken))
        {
            return TypedResults.BadRequest("Failed to parse access token from Discord response.");
        }

        accessToken = tokenResult.AccessToken;

        // fetch the user's identity from Discord
        using var request = new HttpRequestMessage(HttpMethod.Get, $"{_discordApiBaseUrl}/users/@me");
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);

        var userResponse = await _httpClient.SendAsync(request);
        if (!userResponse.IsSuccessStatusCode)
        {
            return TypedResults.BadRequest($"Failed to retrieve user info from Discord. Status: {userResponse.StatusCode}");
        }

        var discordUser = await userResponse.Content.ReadFromJsonAsync<DiscordUserResult>(
            new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower }
        );
        if (discordUser is null)
        {
            return TypedResults.BadRequest("Invalid user info received from Discord.");
        }

        if (!long.TryParse(req.GuildId, out var parsedGuildId))
        {
            return TypedResults.BadRequest($"Invalid GuildId format: {req.GuildId}");
        }

        if (!long.TryParse(discordUser.Id, out var parsedUserId))
        {
            return TypedResults.BadRequest($"Invalid UserId format from Discord: {discordUser.Id}");
        }

        await UpsertUserAndGuild(db, parsedUserId, discordUser.Username, discordUser.Avatar, parsedGuildId);

        var sessionToken = jwtService.GenerateToken(parsedUserId, parsedGuildId);
        return TypedResults.Ok(new TokenResponse(accessToken, sessionToken));
    }

    private static async Task UpsertUserAndGuild(AppDbContext db, long userId, string username, string? avatar, long guildId)
    {
        var user = await db.Users.FirstOrDefaultAsync(u => u.Id == userId);
        if (user is null)
        {
            db.Users.Add(new User { Id = userId, Username = username, Avatar = avatar });
        }
        else
        {
            user.Username = username;
            user.Avatar = avatar;
        }

        if (!await db.Guilds.AnyAsync(g => g.Id == guildId))
        {
            db.Guilds.Add(new Guild { Id = guildId });
        }

        await db.SaveChangesAsync();
    }

    // TODO: break this up into smaller methods, create a file for storing shared static constants across
    // Discord services
    // See how interactions work:
    // https://docs.discord.com/developers/interactions/overview
    private static async Task<IResult> HandleInteraction(HttpContext context, AppDbContext db, IConfiguration config, DiscordWebhookService webhookService)
    {
        var body = await new StreamReader(context.Request.Body).ReadToEndAsync();
        var signature = context.Request.Headers["X-Signature-Ed25519"].FirstOrDefault();
        var timestamp = context.Request.Headers["X-Signature-Timestamp"].FirstOrDefault();

        if (string.IsNullOrEmpty(signature) || string.IsNullOrEmpty(timestamp))
        {
            return Results.Unauthorized();
        }

        var publicKeyHex = config["DISCORD_PUBLIC_KEY"]
            ?? throw new InvalidOperationException("DISCORD_PUBLIC_KEY is not configured.");

        if (!VerifySignature(publicKeyHex, signature, timestamp, body))
        {
            return Results.Unauthorized();
        }

        using var doc = JsonDocument.Parse(body);
        var root = doc.RootElement;
        var type = root.GetProperty("type").GetInt32();

        // ping pong check
        if (type == 1)
        {
            return Results.Json(new { type = 1 });
        }

        // handle slash commands
        if (type == 2)
        {
            if (!root.TryGetProperty("data", out var data) ||
                !data.TryGetProperty("name", out var nameProp))
            {
                return Results.BadRequest("Malformed interaction payload.");
            }

            var commandName = nameProp.GetString();

            if (!root.TryGetProperty("guild_id", out var guildIdProp) ||
                !long.TryParse(guildIdProp.GetString(), out var guildId))
            {
                return Results.Json(new
                {
                    type = 4,
                    data = new { content = "❌ This command can only be used inside a server.", flags = 64 }
                });
            }

            return commandName switch
            {
                "subscribe" => await HandleSubscribeCommandAsync(data, guildId, db, webhookService),
                "unsubscribe" => await HandleUnsubscribeCommandAsync(guildId, db),
                _ => Results.Json(new { type = 12 })
            };
        }

        return Results.BadRequest("Unknown interaction type.");
    }

    private static async Task<IResult> HandleSubscribeCommandAsync(
        JsonElement data,
        long guildId,
        AppDbContext db,
        DiscordWebhookService webhookService)
    {
        var guild = await db.Guilds.FindAsync(guildId);
        if (guild is null)
        {
            return Results.Json(new
            {
                type = 4,
                data = new { content = "❌ Your server is not registered with Bordle yet. Start a game first!", flags = 64 }
            });
        }

        string? webhookUrl = null;
        if (data.TryGetProperty("options", out var options))
        {
            foreach (var option in options.EnumerateArray())
            {
                if (option.TryGetProperty("name", out var optName) &&
                    optName.GetString() == "webhook_url" &&
                    option.TryGetProperty("value", out var optVal))
                {
                    webhookUrl = optVal.GetString();
                    break;
                }
            }
        }

        bool usingSavedUrl = false;
        if (string.IsNullOrWhiteSpace(webhookUrl))
        {
            if (string.IsNullOrWhiteSpace(guild.WebhookUrl))
            {
                return Results.Json(new
                {
                    type = 4,
                    data = new { content = "❌ No saved webhook found. Please provide a webhook URL in the command options.", flags = 64 }
                });
            }

            webhookUrl = guild.WebhookUrl;
            usingSavedUrl = true;
        }
        else if (!webhookUrl.StartsWith($"{_discordApiBaseUrl}/webhooks/"))
        {
            return Results.Json(new
            {
                type = 4,
                data = new { content = "❌ Invalid webhook URL provided.", flags = 64 }
            });
        }

        bool pingSuccess = await webhookService.SendTestPingAsync(webhookUrl);
        if (!pingSuccess)
        {
            guild.WebhookUrl = null;
            guild.IsSubscribed = false;
            await db.SaveChangesAsync();

            return Results.Json(new
            {
                type = 4,
                data = new { content = "❌ The webhook URL is invalid or was deleted. Please provide a new one.", flags = 64 }
            });
        }

        guild.WebhookUrl = webhookUrl;
        guild.IsSubscribed = true;
        await db.SaveChangesAsync();

        var successMessage = usingSavedUrl
            ? "✅ Resubscribed using your previously saved webhook URL!"
            : "✅ Subscribed! You'll receive puzzle streak notifications in this channel.";

        return Results.Json(new
        {
            type = 4,
            data = new { content = successMessage }
        });
    }

    private static async Task<IResult> HandleUnsubscribeCommandAsync(long guildId, AppDbContext db)
    {
        var guild = await db.Guilds.FindAsync(guildId);
        if (guild is not null)
        {
            guild.IsSubscribed = false;
            await db.SaveChangesAsync();
        }

        return Results.Json(new
        {
            type = 4,
            data = new { content = "✅ Unsubscribed. You'll no longer receive puzzle notifications." }
        });
    }

    // Verify the Ed25519 signature of the incoming request from Discord
    private static bool VerifySignature(string publicKeyHex, string signature, string timestamp, string body)
    {
        try
        {
            var algo = SignatureAlgorithm.Ed25519;
            var pubKeyBytes = Convert.FromHexString(publicKeyHex);
            var pubKey = PublicKey.Import(algo, pubKeyBytes, KeyBlobFormat.RawPublicKey);

            var signatureBytes = Convert.FromHexString(signature);
            var msg = Encoding.UTF8.GetBytes(timestamp + body);

            return algo.Verify(pubKey, msg, signatureBytes);
        }
        catch
        {
            return false;
        }
    }
}

internal sealed record TokenRequest(string Code, string GuildId);
internal sealed record TokenResponse(
    string AccessToken,
    string SessionToken
);

internal sealed record DiscordTokenResult(
    string AccessToken
);
internal sealed record DiscordUserResult(string Id, string Username, string? Avatar);