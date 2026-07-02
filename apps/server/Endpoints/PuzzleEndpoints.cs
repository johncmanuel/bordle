using System.Security.Claims;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using Bordle.Server.Data;
using Bordle.Server.Data.Models;

public static class PuzzleEndpoints
{
    public static void RegisterPuzzleEndpoints(this WebApplication app)
    {
        var puzzles = app.MapGroup("/api/puzzles")
            .WithTags("Puzzle endpoints")
            .RequireAuthorization();

        puzzles.MapGet("/daily", GetDailyPuzzle);
        puzzles.MapPost("/{id}/guess", SubmitGuess);
    }

    private static async Task<Results<Ok<DailyPuzzleResponse>, NotFound<string>, UnauthorizedHttpResult>> GetDailyPuzzle(
        AppDbContext db,
        ClaimsPrincipal user)
    {
        var userIdClaim = user.FindFirstValue("userId");
        var guildIdClaim = user.FindFirstValue("guildId");

        if (userIdClaim is null || guildIdClaim is null)
        {
            return TypedResults.Unauthorized();
        }

        var userId = long.Parse(userIdClaim);
        var guildId = long.Parse(guildIdClaim);

        var now = DateTime.UtcNow;

        var puzzle = await db.Puzzles
            .Include(p => p.Submission)
            .Where(p => p.GuildId == guildId && p.PublishedAt <= now)
            .OrderByDescending(p => p.PublishedAt)
            .FirstOrDefaultAsync();

        if (puzzle is null)
        {
            return TypedResults.NotFound("No puzzle available yet. Check back soon!");
        }

        var hints = puzzle.SubmissionId.HasValue
            ? puzzle.Submission!.Hints
            : puzzle.GeneratedHints ?? [];

        var prevGuesses = await db.Guesses
            .Where(g => g.PuzzleId == puzzle.Id && g.UserId == userId)
            .OrderBy(g => g.AttemptNumber)
            .Select(g => g.Word)
            .ToListAsync();

        var answerWord = puzzle.SubmissionId.HasValue
            ? puzzle.Submission!.Word
            : puzzle.FallbackWord!;

        var guessResults = prevGuesses.Select(g => new GuessResult(g, ComputeLetterStates(g, answerWord))).ToList();

        var isFinished = prevGuesses.Count >= 6 || prevGuesses.Any(g => g.Equals(answerWord, StringComparison.OrdinalIgnoreCase));
        string? returnAnswer = isFinished ? answerWord : null;
        long? returnAuthorId = isFinished ? puzzle.Submission?.UserId : null;

        return TypedResults.Ok(new DailyPuzzleResponse(
            puzzle.Id,
            hints,
            guessResults,
            isFinished,
            returnAnswer,
            returnAuthorId
        ));
    }

    internal static List<string> ComputeLetterStates(string guess, string answer)
    {
        var states = new string[5];
        var answerChars = answer.ToUpperInvariant().ToCharArray();
        var guessChars = guess.ToUpperInvariant().ToCharArray();
        var used = new bool[5]; // tracks which answer positions have been "consumed"

        for (var i = 0; i < 5; i++)
        {
            if (guessChars[i] == answerChars[i])
            {
                states[i] = "correct";
                used[i] = true;
            }
        }

        for (var i = 0; i < 5; i++)
        {
            if (states[i] is not null) continue;

            var found = false;
            for (var j = 0; j < 5; j++)
            {
                if (!used[j] && guessChars[i] == answerChars[j])
                {
                    states[i] = "present";
                    used[j] = true;
                    found = true;
                    break;
                }
            }

            if (!found)
            {
                states[i] = "absent";
            }
        }

        return [.. states];
    }

    private static async Task<Results<Ok<GuessResponse>, BadRequest<string>, NotFound<string>, UnauthorizedHttpResult>> SubmitGuess(
        int id,
        GuessRequest req,
        AppDbContext db,
        ClaimsPrincipal user)
    {
        var userIdClaim = user.FindFirstValue("userId");
        var guildIdClaim = user.FindFirstValue("guildId");

        if (userIdClaim is null || guildIdClaim is null)
        {
            return TypedResults.Unauthorized();
        }

        var userId = long.Parse(userIdClaim);
        var guildId = long.Parse(guildIdClaim);

        if (string.IsNullOrWhiteSpace(req.Word) || req.Word.Length != 5)
        {
            return TypedResults.BadRequest("Guess must be exactly 5 letters.");
        }

        if (!req.Word.All(char.IsLetter))
        {
            return TypedResults.BadRequest("Guess must contain only letters.");
        }

        var wordUpper = req.Word.ToUpperInvariant();

        var puzzle = await db.Puzzles
            .Include(p => p.Submission)
            .FirstOrDefaultAsync(p => p.Id == id && p.GuildId == guildId);

        if (puzzle is null)
        {
            return TypedResults.NotFound("Puzzle not found.");
        }

        if (puzzle.PublishedAt.Date != DateTime.UtcNow.Date)
        {
            return TypedResults.BadRequest("This puzzle is no longer active.");
        }

        var numExistingGuesses = await db.Guesses
            .CountAsync(g => g.PuzzleId == id && g.UserId == userId);

        if (numExistingGuesses >= 6)
        {
            return TypedResults.BadRequest("You have already used all 6 guesses.");
        }

        var answerWord = puzzle.SubmissionId.HasValue
            ? puzzle.Submission!.Word
            : puzzle.FallbackWord!;

        var alreadySolved = await db.Guesses
            .AnyAsync(g => g.PuzzleId == id && g.UserId == userId && g.Word == answerWord);

        if (alreadySolved)
        {
            return TypedResults.BadRequest("You already solved this puzzle!");
        }

        var guess = new Guess
        {
            PuzzleId = id,
            UserId = userId,
            GuildId = guildId,
            AttemptNumber = (short)(numExistingGuesses + 1),
            Word = wordUpper,
            CreatedAt = DateTime.UtcNow
        };

        db.Guesses.Add(guess);
        await db.SaveChangesAsync();

        var states = ComputeLetterStates(wordUpper, answerWord);
        var isSolved = wordUpper.Equals(answerWord, StringComparison.OrdinalIgnoreCase);
        var isFinished = isSolved || numExistingGuesses + 1 >= 6;

        string? returnAnswer = isFinished ? answerWord : null;
        long? returnAuthorId = isFinished ? puzzle.Submission?.UserId : null;

        return TypedResults.Ok(new GuessResponse(wordUpper, states, isFinished, isSolved, returnAnswer, returnAuthorId));
    }
}

public record DailyPuzzleResponse(int PuzzleId, List<string> Hints, List<GuessResult> Guesses, bool IsFinished, string? Answer, long? AuthorId);
public record GuessResult(string Word, List<string> States);
public record GuessRequest(string Word);
public record GuessResponse(string Word, List<string> States, bool IsFinished, bool IsSolved, string? Answer, long? AuthorId);
