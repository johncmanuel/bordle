using System.Text;
using System.Text.Json;
using DotNetEnv;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using NSec.Cryptography;
using Bordle.Server.Data;
using Bordle.Server.Data.Models;
using Bordle.Server.Services;

// solid resource for organizing minimal APIs in .NET: https://www.tessferrandez.com/blog/2023/10/31/organizing-minimal-apis.html

public static class DiscordEndpoints
{
    private static readonly HttpClient _httpClient = new();
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
        JwtService jwtService)
    {
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

        // Exchange the OAuth code for an access token with Discord
        var content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["client_id"] = Env.GetString("VITE_DISCORD_CLIENT_ID"),
            ["client_secret"] = Env.GetString("CLIENT_SECRET"),
            ["grant_type"] = "authorization_code",
            ["code"] = req.Code
        });

        var tokenResponse = await _httpClient.PostAsync($"{_discordApiBaseUrl}/oauth2/token", content);

        if (!tokenResponse.IsSuccessStatusCode)
        {
            return TypedResults.BadRequest("Failed to retrieve token from Discord.");
        }

        var tokenResult = await tokenResponse.Content.ReadFromJsonAsync<DiscordTokenResult>();
        accessToken = tokenResult!.AccessToken;

        // fetch the user's identity from Discord
        using var request = new HttpRequestMessage(HttpMethod.Get, $"{_discordApiBaseUrl}/users/@me");
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);

        var userResponse = await _httpClient.SendAsync(request);
        if (!userResponse.IsSuccessStatusCode)
        {
            return TypedResults.BadRequest("Failed to retrieve user info from Discord.");
        }

        var discordUser = await userResponse.Content.ReadFromJsonAsync<DiscordUserResult>();
        if (discordUser is null)
        {
            return TypedResults.BadRequest("Invalid user info received from Discord.");
        }

        if (!long.TryParse(req.GuildId, out var parsedGuildId))
        {
            return TypedResults.BadRequest("Invalid info.");
        }

        if (!long.TryParse(discordUser.Id, out var parsedUserId))
        {
            return TypedResults.BadRequest("Invalid info.");
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

    // Slash commands will be a WIP once everything else is working. Just gonna lay out the foundation here.
    // https://docs.discord.com/developers/interactions/overview
    private static async Task<IResult> HandleInteraction(HttpContext context, IConfiguration config)
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

        // handle the /bordle entry point command
        if (type == 2)
        {
            // type 12 = launch activity
            return Results.Json(new { type = 12 });
        }

        return Results.BadRequest("Unknown interaction type.");
    }

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