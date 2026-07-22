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
        var dictService = CreateMockDictionaryService();
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

        var puzzle = await PuzzleGeneratorWorker.CreatePuzzleForGuildAsync(db, dictService, guildId, DateTime.UtcNow.Date);

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
        var dictService = CreateMockDictionaryService();
        var guildId = 1L;

        db.Guilds.Add(new Guild { Id = guildId });
        await db.SaveChangesAsync();

        var puzzle = await PuzzleGeneratorWorker.CreatePuzzleForGuildAsync(db, dictService, guildId, DateTime.UtcNow.Date);

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
        var dictService = CreateMockDictionaryService();
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

        var puzzle = await PuzzleGeneratorWorker.CreatePuzzleForGuildAsync(db, dictService, guildId, DateTime.UtcNow.Date);

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

    [Fact]
    public async Task CreatePuzzle_FirstPuzzleForGuild_SequenceNumberIsOne()
    {
        using var db = CreateInMemoryDb();
        var dictService = CreateMockDictionaryService();
        var guildId = 1L;

        db.Guilds.Add(new Guild { Id = guildId });
        await db.SaveChangesAsync();

        var puzzle = await PuzzleGeneratorWorker.CreatePuzzleForGuildAsync(db, dictService, guildId, DateTime.UtcNow.Date);

        Assert.Equal(1, puzzle.SequenceNumber);
    }

    [Fact]
    public async Task CreatePuzzle_SecondPuzzleForGuild_SequenceNumberIsTwo()
    {
        using var db = CreateInMemoryDb();
        var dictService = CreateMockDictionaryService();
        var guildId = 1L;

        db.Guilds.Add(new Guild { Id = guildId });
        db.Puzzles.Add(new Puzzle
        {
            GuildId = guildId,
            FallbackWord = "APPLE",
            SequenceNumber = 1,
            PublishedAt = DateTime.UtcNow.AddDays(-1),
            ClosedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();

        var puzzle = await PuzzleGeneratorWorker.CreatePuzzleForGuildAsync(db, dictService, guildId, DateTime.UtcNow.Date);

        Assert.Equal(2, puzzle.SequenceNumber);
    }

    [Fact]
    public async Task CreatePuzzle_MultipleGuilds_SequenceNumbersAreIndependent()
    {
        using var db = CreateInMemoryDb();
        var dictService = CreateMockDictionaryService();
        var guildA = 100L;
        var guildB = 200L;

        db.Guilds.Add(new Guild { Id = guildA });
        db.Guilds.Add(new Guild { Id = guildB });

        // Guild A already has 3 puzzles
        for (var i = 1; i <= 3; i++)
        {
            db.Puzzles.Add(new Puzzle
            {
                GuildId = guildA,
                FallbackWord = "APPLE",
                SequenceNumber = i,
                PublishedAt = DateTime.UtcNow.AddDays(-i),
                ClosedAt = DateTime.UtcNow.AddDays(-i + 1)
            });
        }
        await db.SaveChangesAsync();

        // Guild B creates its first puzzle — should be 1, not 4
        var puzzleB = await PuzzleGeneratorWorker.CreatePuzzleForGuildAsync(db, dictService, guildB, DateTime.UtcNow.Date);
        Assert.Equal(1, puzzleB.SequenceNumber);

        // Guild A creates its 4th puzzle — should be 4
        db.Puzzles.Add(puzzleB); // save B first so it's in the DB
        await db.SaveChangesAsync();

        var puzzleA = await PuzzleGeneratorWorker.CreatePuzzleForGuildAsync(db, dictService, guildA, DateTime.UtcNow.Date);
        Assert.Equal(4, puzzleA.SequenceNumber);
    }

    [Fact]
    public async Task CreatePuzzle_SequenceNumberIncrementsAfterSave()
    {
        using var db = CreateInMemoryDb();
        var dictService = CreateMockDictionaryService();
        var guildId = 1L;

        db.Guilds.Add(new Guild { Id = guildId });
        await db.SaveChangesAsync();

        // Create and save first puzzle
        var puzzle1 = await PuzzleGeneratorWorker.CreatePuzzleForGuildAsync(db, dictService, guildId, DateTime.UtcNow.Date);
        Assert.Equal(1, puzzle1.SequenceNumber);
        db.Puzzles.Add(puzzle1);
        await db.SaveChangesAsync();

        // Create second puzzle — should see the first one in the DB
        var puzzle2 = await PuzzleGeneratorWorker.CreatePuzzleForGuildAsync(db, dictService, guildId, DateTime.UtcNow.Date.AddDays(1));
        Assert.Equal(2, puzzle2.SequenceNumber);
        db.Puzzles.Add(puzzle2);
        await db.SaveChangesAsync();

        // Third
        var puzzle3 = await PuzzleGeneratorWorker.CreatePuzzleForGuildAsync(db, dictService, guildId, DateTime.UtcNow.Date.AddDays(2));
        Assert.Equal(3, puzzle3.SequenceNumber);
    }
}

public class DailyStreakTests
{
    private AppDbContext CreateInMemoryDb()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        return new AppDbContext(options);
    }

    private static DictionaryService CreateMockDictionaryService()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(Path.Combine(tempDir, "Data"));
        File.WriteAllLines(Path.Combine(tempDir, "Data", "words.txt"), ["apple", "brave", "crane", "dance", "eagle"]);
        var mockEnv = new Mock<Microsoft.AspNetCore.Hosting.IWebHostEnvironment>();
        mockEnv.Setup(e => e.ContentRootPath).Returns(tempDir);
        var service = new DictionaryService(mockEnv.Object);
        service.InitializeAsync().GetAwaiter().GetResult();
        return service;
    }

    private System.Security.Claims.ClaimsPrincipal CreateMockUser(long userId, long guildId)
    {
        return new System.Security.Claims.ClaimsPrincipal(new System.Security.Claims.ClaimsIdentity(
        [
            new System.Security.Claims.Claim("userId", userId.ToString()),
            new System.Security.Claims.Claim("guildId", guildId.ToString())
        ]));
    }

    [Fact]
    public async Task SubmitGuess_FirstGuess_IncrementsStreak()
    {
        using var db = CreateInMemoryDb();
        var guild = new Guild { Id = 1, DailyStreak = 0 };
        db.Guilds.Add(guild);
        var puzzle = new Puzzle { Id = 1, GuildId = 1, FallbackWord = "apple", PublishedAt = DateTime.UtcNow, ClosedAt = DateTime.UtcNow.AddDays(1), SequenceNumber = 1 };
        db.Puzzles.Add(puzzle);
        await db.SaveChangesAsync();

        var dictService = CreateMockDictionaryService();
        var user = CreateMockUser(1, 1);
        var req = new GuessRequest("apple");
        await PuzzleEndpoints.SubmitGuess(puzzle.Id, req, db, user);

        var updatedGuild = await db.Guilds.FindAsync(1L);
        Assert.Equal(1, updatedGuild!.DailyStreak);
    }

    [Fact]
    public async Task SubmitGuess_SecondGuess_DoesNotIncrementStreak()
    {
        using var db = CreateInMemoryDb();
        var guild = new Guild { Id = 1, DailyStreak = 1 }; // already incremented
        db.Guilds.Add(guild);
        var puzzle = new Puzzle { Id = 1, GuildId = 1, FallbackWord = "apple", PublishedAt = DateTime.UtcNow, ClosedAt = DateTime.UtcNow.AddDays(1), SequenceNumber = 1 };
        db.Puzzles.Add(puzzle);
        db.Guesses.Add(new Guess { PuzzleId = 1, GuildId = 1, UserId = 2, AttemptNumber = 1, Word = "CRANE", CreatedAt = DateTime.UtcNow });
        await db.SaveChangesAsync();

        var dictService = CreateMockDictionaryService();
        var user = CreateMockUser(1, 1);
        var req = new GuessRequest("brave");
        await PuzzleEndpoints.SubmitGuess(puzzle.Id, req, db, user);

        var updatedGuild = await db.Guilds.FindAsync(1L);
        Assert.Equal(1, updatedGuild!.DailyStreak); // still 1
    }

    [Fact]
    public async Task GenerateMissingPuzzlesAsync_NoGuesses_ResetsStreak()
    {
        using var db = CreateInMemoryDb();
        var guild = new Guild { Id = 1, DailyStreak = 5 }; 
        db.Guilds.Add(guild);
        
        var minute = DateTime.UtcNow.Minute / 2 * 2;
        var todayUtc = new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, DateTime.UtcNow.Day, DateTime.UtcNow.Hour, minute, 0, DateTimeKind.Utc);

        var prevPuzzle = new Puzzle { Id = 1, GuildId = 1, FallbackWord = "apple", PublishedAt = todayUtc.AddMinutes(-2), ClosedAt = todayUtc, SequenceNumber = 1 };
        db.Puzzles.Add(prevPuzzle);
        await db.SaveChangesAsync();

        var mockServiceProvider = new Mock<IServiceProvider>();
        var mockServiceScope = new Mock<Microsoft.Extensions.DependencyInjection.IServiceScope>();
        var mockServiceScopeFactory = new Mock<Microsoft.Extensions.DependencyInjection.IServiceScopeFactory>();

        mockServiceProvider.Setup(s => s.GetService(typeof(Microsoft.Extensions.DependencyInjection.IServiceScopeFactory))).Returns(mockServiceScopeFactory.Object);
        mockServiceScopeFactory.Setup(s => s.CreateScope()).Returns(mockServiceScope.Object);
        mockServiceScope.Setup(s => s.ServiceProvider).Returns(mockServiceProvider.Object);
        mockServiceProvider.Setup(s => s.GetService(typeof(AppDbContext))).Returns(db);

        var worker = new PuzzleGeneratorWorker(mockServiceProvider.Object, CreateMockDictionaryService(), new Mock<ILogger<PuzzleGeneratorWorker>>().Object);
        
        await worker.GenerateMissingPuzzlesAsync(CancellationToken.None);

        var updatedGuild = await db.Guilds.FindAsync(1L);
        Assert.Equal(0, updatedGuild!.DailyStreak); 
    }

    [Fact]
    public async Task GenerateMissingPuzzlesAsync_WithGuesses_PreservesStreak()
    {
        using var db = CreateInMemoryDb();
        var guild = new Guild { Id = 1, DailyStreak = 5 }; 
        db.Guilds.Add(guild);
        
        var minute = DateTime.UtcNow.Minute / 2 * 2;
        var todayUtc = new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, DateTime.UtcNow.Day, DateTime.UtcNow.Hour, minute, 0, DateTimeKind.Utc);

        var prevPuzzle = new Puzzle { Id = 1, GuildId = 1, FallbackWord = "apple", PublishedAt = todayUtc.AddMinutes(-2), ClosedAt = todayUtc, SequenceNumber = 1 };
        db.Puzzles.Add(prevPuzzle);
        db.Guesses.Add(new Guess { PuzzleId = 1, GuildId = 1, UserId = 1, AttemptNumber = 1, Word = "APPLE", CreatedAt = todayUtc.AddMinutes(-1) });
        await db.SaveChangesAsync();

        var mockServiceProvider = new Mock<IServiceProvider>();
        var mockServiceScope = new Mock<Microsoft.Extensions.DependencyInjection.IServiceScope>();
        var mockServiceScopeFactory = new Mock<Microsoft.Extensions.DependencyInjection.IServiceScopeFactory>();

        mockServiceProvider.Setup(s => s.GetService(typeof(Microsoft.Extensions.DependencyInjection.IServiceScopeFactory))).Returns(mockServiceScopeFactory.Object);
        mockServiceScopeFactory.Setup(s => s.CreateScope()).Returns(mockServiceScope.Object);
        mockServiceScope.Setup(s => s.ServiceProvider).Returns(mockServiceProvider.Object);
        mockServiceProvider.Setup(s => s.GetService(typeof(AppDbContext))).Returns(db);

        var worker = new PuzzleGeneratorWorker(mockServiceProvider.Object, CreateMockDictionaryService(), new Mock<ILogger<PuzzleGeneratorWorker>>().Object);
        
        await worker.GenerateMissingPuzzlesAsync(CancellationToken.None);

        var updatedGuild = await db.Guilds.FindAsync(1L);
        Assert.Equal(5, updatedGuild!.DailyStreak); // still 5
    }
}
