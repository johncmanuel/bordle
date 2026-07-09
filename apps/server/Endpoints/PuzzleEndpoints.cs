using System.Security.Claims;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using Bordle.Server.Data;
using Bordle.Server.Data.Models;
using Bordle.Server.Services;

public static class PuzzleEndpoints
{
    public static void RegisterPuzzleEndpoints(this WebApplication app)
    {
        var puzzles = app.MapGroup("/api/puzzles")
            .WithTags("Puzzle endpoints")
            .RequireAuthorization();

        puzzles.MapGet("/daily", GetDailyPuzzle);
        puzzles.MapPost("/{id}/guess", SubmitGuess);
        puzzles.MapGet("/{id}/players", GetPuzzlePlayers);
    }

    private static async Task<Results<Ok<DailyPuzzleResponse>, BadRequest<string>, NotFound<string>, UnauthorizedHttpResult>> GetDailyPuzzle(
        AppDbContext db,
        ClaimsPrincipal user,
        DictionaryService dictionaryService)
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

        var now = DateTime.UtcNow;

        var puzzle = await db.Puzzles
            .Include(p => p.Submission)
            .ThenInclude(s => s!.User)
            .Where(p => p.GuildId == guildId && p.PublishedAt <= now)
            .OrderByDescending(p => p.PublishedAt)
            .FirstOrDefaultAsync();

        // create a puzzle if there aren't any for today yet
        // resolves the issue where upon first startup ever, there are no puzzles in the database and the user can't play until the next day
        // TODO: reuse logic in a similar function (it's already in PuzzleGeneratorWorker.cs) to avoid duplication
        if (puzzle is null)
        {
#if DEBUG
            var minute = now.Minute / 2 * 2;
            var todayUtc = new DateTime(now.Year, now.Month, now.Day, now.Hour, minute, 0, DateTimeKind.Utc);
#else
            var todayUtc = now.Date;
#endif
            puzzle = await PuzzleGeneratorWorker.CreatePuzzleForGuildAsync(db, dictionaryService, guildId, todayUtc);

            db.Puzzles.Add(puzzle);
            await db.SaveChangesAsync();

            puzzle = await db.Puzzles
                .Include(p => p.Submission)
                .ThenInclude(s => s!.User)
                .FirstOrDefaultAsync(p => p.Id == puzzle.Id);
                
            if (puzzle is null)
            {
                return TypedResults.NotFound("Failed to generate puzzle.");
            }
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
        string? returnAuthorUsername = isFinished ? puzzle.Submission?.User?.Username : null;

        return TypedResults.Ok(new DailyPuzzleResponse(
            puzzle.Id,
            puzzle.SequenceNumber,
            hints,
            guessResults,
            isFinished,
            returnAnswer,
            returnAuthorUsername
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
            return TypedResults.BadRequest("Guess must be exactly 5 letters.");
        }

        if (!req.Word.All(char.IsLetter))
        {
            return TypedResults.BadRequest("Guess must contain only letters.");
        }

        var wordUpper = req.Word.ToUpperInvariant();

        var puzzle = await db.Puzzles
            .Include(p => p.Submission)
            .ThenInclude(s => s!.User)
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
        string? returnAuthorUsername = isFinished ? puzzle.Submission?.User?.Username : null;

        return TypedResults.Ok(new GuessResponse(wordUpper, states, isFinished, isSolved, returnAnswer, returnAuthorUsername));
    }

    // Gets the list of players and their guesses for a specific puzzle, excluding the current user, in the same guild.
    private static async Task<Results<Ok<PuzzlePlayersResponse>, BadRequest<string>, NotFound<string>, UnauthorizedHttpResult>> GetPuzzlePlayers(
        int id,
        AppDbContext db,
        ClaimsPrincipal user)
    {
        var userIdClaim = user.FindFirstValue("userId");
        var guildIdClaim = user.FindFirstValue("guildId");

        if (userIdClaim is null || guildIdClaim is null)
        {
            return TypedResults.Unauthorized();
        }

        if (!long.TryParse(guildIdClaim, out var guildId))
        {
            return TypedResults.BadRequest("Invalid data.");
        }

        if (!long.TryParse(userIdClaim, out var userId))
        {
            return TypedResults.BadRequest("Invalid data.");
        }

        var puzzle = await db.Puzzles
            .Include(p => p.Submission)
            .FirstOrDefaultAsync(p => p.Id == id && p.GuildId == guildId);

        if (puzzle is null)
        {
            return TypedResults.NotFound("Puzzle not found.");
        }

        var answerWord = puzzle.SubmissionId.HasValue
            ? puzzle.Submission!.Word
            : puzzle.FallbackWord!;

        var allGuesses = await db.Guesses
            .Include(g => g.User)
            .Where(g => g.PuzzleId == id && g.GuildId == guildId && g.UserId != userId)
            .OrderBy(g => g.UserId)
            .ThenBy(g => g.AttemptNumber)
            .ToListAsync();

        var playerStates = allGuesses
            .GroupBy(g => g.UserId)
            .Select(group => new PlayerState(
                group.Key.ToString(),
                group.First().User?.Username ?? "Unknown User",
                group.First().User?.Avatar,
                [.. group.Select(g => ComputeLetterStates(g.Word, answerWord))]
            ))
            .ToList();

        return TypedResults.Ok(new PuzzlePlayersResponse(playerStates));
    }
}

public record DailyPuzzleResponse(int PuzzleId, int SequenceNumber, List<string> Hints, List<GuessResult> Guesses, bool IsFinished, string? Answer, string? AuthorUsername);
public record GuessResult(string Word, List<string> States);
public record GuessRequest(string Word);
public record GuessResponse(string Word, List<string> States, bool IsFinished, bool IsSolved, string? Answer, string? AuthorUsername);
public record PuzzlePlayersResponse(List<PlayerState> Players);
public record PlayerState(string UserId, string Username, string? Avatar, List<List<string>> GuessStates);
