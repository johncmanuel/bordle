using Microsoft.EntityFrameworkCore;
using Bordle.Server.Data;
using Bordle.Server.Data.Models;

namespace Bordle.Server.Services
{
    public class PuzzleGeneratorWorker(
        IServiceProvider serviceProvider,
        DictionaryService dictionaryService,
        ILogger<PuzzleGeneratorWorker> logger) : BackgroundService

    {
        private static readonly TimeSpan CheckInterval = TimeSpan.FromMinutes(1);

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await GenerateMissingPuzzlesAsync(stoppingToken);
                    logger.LogInformation("Puzzle generation cycle completed successfully.");
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Error during puzzle generation cycle");
                }

                await Task.Delay(CheckInterval, stoppingToken);
            }
        }

        internal async Task GenerateMissingPuzzlesAsync(CancellationToken ct)
        {
            using var scope = serviceProvider.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

#if DEBUG
            // generate every 2 minutes just for faster iteration
            // can be changed as needed
            var minute = (DateTime.UtcNow.Minute / 2) * 2;
            var todayUtc = new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, DateTime.UtcNow.Day, DateTime.UtcNow.Hour, minute, 0, DateTimeKind.Utc);
#else
            var todayUtc = DateTime.UtcNow.Date;
#endif

            var guildsWithoutPuzzle = await db.Guilds
#if DEBUG
                    .Where(g => !db.Puzzles.Any(p => p.GuildId == g.Id && p.PublishedAt == todayUtc))
#else
                    .Where(g => !db.Puzzles.Any(p => p.GuildId == g.Id && p.PublishedAt.Date == todayUtc))
#endif
                .ToListAsync(ct);

            if (guildsWithoutPuzzle.Count == 0)
            {
                return;
            }

            logger.LogInformation("Generating puzzles for {Count} guild(s)", guildsWithoutPuzzle.Count);

            foreach (var guild in guildsWithoutPuzzle)
            {
                var puzzle = await CreatePuzzleForGuildAsync(db, dictionaryService, guild.Id, todayUtc);
                db.Puzzles.Add(puzzle);
                logger.LogInformation("Generated puzzle for guild {GuildId}: {Word}",
                    guild.Id, puzzle.Submission?.Word ?? puzzle.FallbackWord);
            }

            await db.SaveChangesAsync(ct);
        }

        internal static async Task<Puzzle> CreatePuzzleForGuildAsync(AppDbContext db, DictionaryService dictionaryService, long guildId, DateTime publishDate)
        {
            var lastPuzzle = await db.Puzzles
                .Include(p => p.Guesses)
                .Where(p => p.GuildId == guildId)
                .OrderByDescending(p => p.SequenceNumber)
                .FirstOrDefaultAsync();

            if (lastPuzzle != null)
            {
                bool noGuessesOnLastPuzzle = lastPuzzle.Guesses.Count == 0;
#if DEBUG
                bool missedInterval = lastPuzzle.PublishedAt < publishDate.AddMinutes(-2);
#else
                bool missedInterval = lastPuzzle.PublishedAt.Date < publishDate.AddDays(-1).Date;
#endif
                if (noGuessesOnLastPuzzle || missedInterval)
                {
                    var guildToUpdate = await db.Guilds.FindAsync(guildId);
                    if (guildToUpdate != null)
                    {
                        guildToUpdate.DailyStreak = 0;
                    }
                }
            }

            // calculate the next sequence number for a guild
            var maxSequenceNum = lastPuzzle?.SequenceNumber ?? 0;
            var nextSequenceNum = maxSequenceNum + 1;

            // find an unused submission from the given guild and check if it exists
            // if not then fall back to the dictionary 
            var unusedSubmission = await db.WordSubmissions
                .Where(ws => ws.GuildId == guildId && !ws.IsUsed)
                .OrderBy(_ => Guid.NewGuid())
                .FirstOrDefaultAsync();

            if (unusedSubmission is not null)
            {
                unusedSubmission.IsUsed = true;

                return new Puzzle
                {
                    GuildId = guildId,
                    SubmissionId = unusedSubmission.Id,
                    SequenceNumber = nextSequenceNum,
                    PublishedAt = publishDate,
#if DEBUG
                    ClosedAt = publishDate.AddMinutes(2),
#else
                    ClosedAt = publishDate.AddDays(1),
#endif
                };
            }

            var fallbackWord = await dictionaryService.GetRandomWordAsync();
            var hints = GenerateStructuralHints(fallbackWord);

            return new Puzzle
            {
                GuildId = guildId,
                FallbackWord = fallbackWord,
                GeneratedHints = hints,
                SequenceNumber = nextSequenceNum,
                PublishedAt = publishDate,
#if DEBUG
                ClosedAt = publishDate.AddMinutes(2),
#else
                ClosedAt = publishDate.AddDays(1),
#endif
            };
        }

        internal static List<string> GenerateStructuralHints(string word)
        {
            var hints = new List<string>();
            var upper = word.ToUpperInvariant();
            var vowels = upper.Count("AEIOU".Contains);

            // the only hints i could think of
            hints.Add($"Starts with '{upper[0]}'");
            hints.Add($"Has {vowels} vowel{(vowels != 1 ? "s" : "")}");
            hints.Add($"Ends with '{upper[^1]}'");

            return hints;
        }
    }
}
