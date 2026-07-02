using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using Bordle.Server.Data;
using Bordle.Server.Data.Models;
using Bordle.Server.Services;

namespace server.tests;

public class PuzzleGeneratorTests
{
    private AppDbContext CreateInMemoryDb()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        return new AppDbContext(options);
    }

    private PuzzleGeneratorWorker CreateWorker(DictionaryService? dictionaryService = null)
    {
        var mockServiceProvider = new Mock<IServiceProvider>();
        var mockLogger = new Mock<ILogger<PuzzleGeneratorWorker>>();

        return new PuzzleGeneratorWorker(
            mockServiceProvider.Object,
            dictionaryService ?? CreateMockDictionaryService(),
            mockLogger.Object);
    }

    private static DictionaryService CreateMockDictionaryService()
    {
        // Create a temporary words file for testing
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(Path.Combine(tempDir, "Data"));
        var wordsPath = Path.Combine(tempDir, "Data", "words.txt");
        File.WriteAllLines(wordsPath, ["apple", "brave", "crane", "dance", "eagle"]);

        var mockEnv = new Mock<Microsoft.AspNetCore.Hosting.IWebHostEnvironment>();
        mockEnv.Setup(e => e.ContentRootPath).Returns(tempDir);

        var service = new DictionaryService(mockEnv.Object);
        service.InitializeAsync().GetAwaiter().GetResult();
        return service;
    }

    [Fact]
    public async Task CreatePuzzle_WithUnusedSubmission_UsesSubmission()
    {
        using var db = CreateInMemoryDb();
        var guildId = 1L;

        db.Guilds.Add(new Guild { Id = guildId });
        db.Users.Add(new User { Id = 1L });
        db.WordSubmissions.Add(new WordSubmission
        {
            GuildId = guildId,
            UserId = 1L,
            Word = "HELLO",
            Hints = ["Greeting", "Common word"],
            SubmittedAt = DateTime.UtcNow,
            IsUsed = false
        });
        await db.SaveChangesAsync();

        var worker = CreateWorker();

        var puzzle = await worker.CreatePuzzleForGuildAsync(db, guildId, DateTime.UtcNow.Date);

        Assert.NotNull(puzzle.SubmissionId);
        Assert.Null(puzzle.FallbackWord);
        Assert.Null(puzzle.GeneratedHints);

        var submission = await db.WordSubmissions.FindAsync(puzzle.SubmissionId);
        Assert.True(submission!.IsUsed);
    }

    [Fact]
    public async Task CreatePuzzle_WithNoSubmissions_UsesFallbackDictionary()
    {
        using var db = CreateInMemoryDb();
        var guildId = 1L;

        db.Guilds.Add(new Guild { Id = guildId });
        await db.SaveChangesAsync();

        var worker = CreateWorker();

        var puzzle = await worker.CreatePuzzleForGuildAsync(db, guildId, DateTime.UtcNow.Date);

        Assert.Null(puzzle.SubmissionId);
        Assert.NotNull(puzzle.FallbackWord);
        Assert.Equal(5, puzzle.FallbackWord!.Length);
        Assert.NotNull(puzzle.GeneratedHints);
        Assert.Equal(3, puzzle.GeneratedHints!.Count);
    }

    [Fact]
    public async Task CreatePuzzle_SkipsAlreadyUsedSubmissions()
    {
        using var db = CreateInMemoryDb();
        var guildId = 1L;

        db.Guilds.Add(new Guild { Id = guildId });
        db.Users.Add(new User { Id = 1L });

        db.WordSubmissions.Add(new WordSubmission
        {
            GuildId = guildId,
            UserId = 1L,
            Word = "STALE",
            Hints = [],
            SubmittedAt = DateTime.UtcNow.AddDays(-1),
            IsUsed = true
        });
        await db.SaveChangesAsync();

        var worker = CreateWorker();

        var puzzle = await worker.CreatePuzzleForGuildAsync(db, guildId, DateTime.UtcNow.Date);

        // should fall back to dictionary since the only submission is used
        Assert.Null(puzzle.SubmissionId);
        Assert.NotNull(puzzle.FallbackWord);
    }

    [Fact]
    public void GenerateStructuralHints_ProducesThreeHints()
    {
        var hints = PuzzleGeneratorWorker.GenerateStructuralHints("CRANE");

        Assert.Equal(3, hints.Count);
        Assert.Contains("'C'", hints[0]); // starts with C
        Assert.Contains("2", hints[1]);    // has 2 vowels (A, E)
        Assert.Contains("'E'", hints[2]); // ends with E
    }

    [Fact]
    public void ComputeLetterStates_AllCorrect()
    {
        var states = PuzzleEndpoints.ComputeLetterStates("CRANE", "CRANE");
        Assert.All(states, s => Assert.Equal("correct", s));
    }

    [Fact]
    public void ComputeLetterStates_AllAbsent()
    {
        var states = PuzzleEndpoints.ComputeLetterStates("BUXOM", "CRANE");
        Assert.All(states, s => Assert.Equal("absent", s));
    }

    [Fact]
    public void ComputeLetterStates_MixedStates()
    {
        // Guess "CARES" vs answer "CRANE"
        // C = correct, A = present (at pos 2 in answer), R = present (at pos 1 in answer), E = present (at pos 4 in answer), S = absent
        var states = PuzzleEndpoints.ComputeLetterStates("CARES", "CRANE");
        Assert.Equal("correct", states[0]); // C
        Assert.Equal("present", states[1]); // A
        Assert.Equal("present", states[2]); // R
        Assert.Equal("present", states[3]); // E
        Assert.Equal("absent", states[4]);  // S
    }

    [Fact]
    public void ComputeLetterStates_DuplicateLetters_HandledCorrectly()
    {
        // Guess "SPEED" vs answer "ABIDE"
        // S = absent, P = absent, E = present (matches the E in ABIDE pos 4), E = absent (already consumed), D = present
        var states = PuzzleEndpoints.ComputeLetterStates("SPEED", "ABIDE");
        Assert.Equal("absent", states[0]);  // S
        Assert.Equal("absent", states[1]);  // P
        Assert.Equal("present", states[2]); // E (matches pos 4 E in ABIDE)
        Assert.Equal("absent", states[3]);  // E (duplicate, already consumed)
        Assert.Equal("present", states[4]); // D
    }
}
