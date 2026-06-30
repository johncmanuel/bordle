using DotNetEnv;
using Microsoft.AspNetCore.Http.HttpResults;

// solid resource for organizing minimal APIs in .NET: https://www.tessferrandez.com/blog/2023/10/31/organizing-minimal-apis.html

public static class DiscordEndpoints
{
    private static readonly HttpClient _httpClient = new();
    private static readonly string _DiscordApiBaseUrl = "https://discord.com/api";

    public static void RegisterDiscordEndpoints(this WebApplication app)
    {
        var discord = app.MapGroup("/discord").WithTags("Discord endpoints");

        discord.MapPost("/token", GetDiscordToken);
    }

    private static async Task<Results<Ok<TokenResponse>, BadRequest<string>>> GetDiscordToken(TokenRequest req)
    {
        // If the client is using DiscordSDKMock, it sends a "mock_code". Return a mock token response in that case.
        if (req.code == "mock_code")
        {
            return TypedResults.Ok(new TokenResponse("mock_token"));
        }

        var content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["client_id"] = Env.GetString("VITE_CLIENT_ID"),
            ["client_secret"] = Env.GetString("CLIENT_SECRET"),
            ["grant_type"] = "authorization_code",
            ["code"] = req.code
        });

        var response = await _httpClient.PostAsync($"{_DiscordApiBaseUrl}/oauth2/token", content);

        if (!response.IsSuccessStatusCode)
        {
            return TypedResults.BadRequest("Failed to retrieve token from Discord.");
        }

        var result = await response.Content.ReadFromJsonAsync<TokenResponse>();
        return TypedResults.Ok(result!);
    }
}

public record TokenResponse(string access_token);
public record TokenRequest(string code);