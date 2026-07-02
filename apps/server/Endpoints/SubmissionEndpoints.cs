using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Http.HttpResults;
using Bordle.Server.Data;
using Bordle.Server.Data.Models;

public static class SubmissionEndpoints
{
    public static void RegisterSubmissionEndpoints(this WebApplication app)
    {
        var submissions = app.MapGroup("/api/submissions")
            .WithTags("Submission endpoints")
            .RequireAuthorization();

        submissions.MapPost("/", SubmitWord);
    }

    private static async Task<Results<Ok<SubmissionResponse>, BadRequest<string>, UnauthorizedHttpResult>> SubmitWord(
        SubmitWordRequest req,
        AppDbContext db,
        ClaimsPrincipal user)
    {
        var userIdClaim = user.FindFirstValue("userId");
        var guildIdClaim = user.FindFirstValue("guildId");

        if (userIdClaim is null || guildIdClaim is null)
        {
            return TypedResults.Unauthorized();
        }

        if (!long.TryParse(userIdClaim, out var userId))
        {
            return TypedResults.BadRequest("Invalid data.");
        }

        if (!long.TryParse(guildIdClaim, out var guildId))
        {
            return TypedResults.BadRequest("Invalid data.");
        }

        if (string.IsNullOrWhiteSpace(req.Word) || req.Word.Length != 5)
        {
            return TypedResults.BadRequest("Word must be exactly 5 letters.");
        }

        if (!req.Word.All(char.IsLetter))
        {
            return TypedResults.BadRequest("Word must contain only letters.");
        }

        var wordUpper = req.Word.ToUpperInvariant();

        // Check if the user has already submitted this word
        // It's fine if other users already use the same word but we don't want the same user to submit the same word multiple times
        var alreadyExists = await db.WordSubmissions
            .AnyAsync(ws => ws.GuildId == guildId && ws.UserId == userId && ws.Word == wordUpper);

        if (alreadyExists)
        {
            return TypedResults.BadRequest($"You have already submitted the word '{wordUpper}'! Try another one.");
        }

        var hints = req.Hints ?? [];
        if (hints.Count > 3)
        {
            return TypedResults.BadRequest("Maximum of 3 hints allowed.");
        }

        if (hints.Any(h => string.IsNullOrWhiteSpace(h) || h.Length > 25))
        {
            return TypedResults.BadRequest("Each hint must be between 1 and 25 characters.");
        }

        var submission = new WordSubmission
        {
            GuildId = guildId,
            UserId = userId,
            Word = wordUpper,
            Hints = hints,
            SubmittedAt = DateTime.UtcNow,
            IsUsed = false
        };

        db.WordSubmissions.Add(submission);
        await db.SaveChangesAsync();

        return TypedResults.Ok(new SubmissionResponse(submission.Id, submission.Word, submission.Hints));
    }
}

public record SubmitWordRequest(string Word, List<string>? Hints);
public record SubmissionResponse(int Id, string Word, List<string> Hints);
