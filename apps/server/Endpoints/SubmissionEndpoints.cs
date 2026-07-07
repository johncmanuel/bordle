using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Http.HttpResults;
using Bordle.Server.Data;
using Bordle.Server.Data.Models;

public static class SubmissionEndpoints
{
    // a cooldown for already submitted words. users can submit the
    // same word again after submitting a number of different words
    private static readonly int CooldownLimit = 5;

    public static void RegisterSubmissionEndpoints(this WebApplication app)
    {
        var submissions = app.MapGroup("/api/submissions")
            .WithTags("Submission endpoints")
            .RequireAuthorization();

        submissions.MapPost("/", SubmitWord)
            .RequireRateLimiting("WordSubmissionLimit");
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
        var recentWords = await db.WordSubmissions
            .Where(ws => ws.GuildId == guildId && ws.UserId == userId)
            .OrderByDescending(ws => ws.SubmittedAt)
            .Take(CooldownLimit)
            .Select(ws => ws.Word)
            .ToListAsync();

        if (recentWords.Contains(wordUpper))
        {
            return TypedResults.BadRequest($"You have recently submitted the word '{wordUpper}'. Try submitting some different words first!");
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
