using DotNetEnv;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
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
    }

    private static async Task<Results<Ok<TokenResponse>, BadRequest<string>>> GetDiscordToken(
        TokenRequest req,
        AppDbContext db,
        JwtService jwtService)
    {
        string accessToken;

        // If the client is using DiscordSDKMock, it sends a "mock_code". Return a mock token response in that case.
        if (req.Code == "mock_code")
        {
            // add mock user and guild 
            const long mockUserId = 1L;
            const long mockGuildId = 1L;

            await UpsertUserAndGuild(db, mockUserId, mockGuildId);

            var mockSessionToken = jwtService.GenerateToken(mockUserId, mockGuildId);
            return TypedResults.Ok(new TokenResponse("mock_token", mockSessionToken));
        }

        // Exchange the OAuth code for an access token with Discord
        var content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["client_id"] = Env.GetString("VITE_CLIENT_ID"),
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
            return TypedResults.BadRequest("Invalid GuildId.");
        }

        await UpsertUserAndGuild(db, discordUser.Id, parsedGuildId);

        var sessionToken = jwtService.GenerateToken(discordUser.Id, parsedGuildId);
        return TypedResults.Ok(new TokenResponse(accessToken, sessionToken));
    }

    private static async Task UpsertUserAndGuild(AppDbContext db, long userId, long guildId)
    {
        if (!await db.Users.AnyAsync(u => u.Id == userId))
        {
            db.Users.Add(new User { Id = userId });
        }

        if (!await db.Guilds.AnyAsync(g => g.Id == guildId))
        {
            db.Guilds.Add(new Guild { Id = guildId });
        }

        await db.SaveChangesAsync();
    }
}

public record TokenRequest(string Code, string GuildId);
public record TokenResponse(
    [property: System.Text.Json.Serialization.JsonPropertyName("access_token")] string AccessToken,
    [property: System.Text.Json.Serialization.JsonPropertyName("session_token")] string SessionToken
);

internal record DiscordTokenResult(
    [property: System.Text.Json.Serialization.JsonPropertyName("access_token")] string AccessToken
);
internal record DiscordUserResult(long Id, string Username);