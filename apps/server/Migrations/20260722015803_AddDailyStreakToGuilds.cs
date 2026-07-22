using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace server.Migrations
{
    /// <inheritdoc />
    public partial class AddDailyStreakToGuilds : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "DailyStreak",
                table: "Guilds",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.Sql(@"
WITH PuzzleGuesses AS (
    SELECT
        p.""GuildId"",
        p.""Id"" as ""PuzzleId"",
        p.""SequenceNumber"",
        (SELECT COUNT(*) FROM ""Guesses"" g WHERE g.""PuzzleId"" = p.""Id"") as ""GuessCount""
    FROM ""Puzzles"" p
),
RankedPuzzles AS (
    SELECT
        ""GuildId"",
        ""SequenceNumber"",
        ""GuessCount"",
        ROW_NUMBER() OVER (PARTITION BY ""GuildId"" ORDER BY ""SequenceNumber"" DESC) as rn
    FROM PuzzleGuesses
),
StreakCalculations AS (
    SELECT
        ""GuildId"",
        (
            SELECT COUNT(*)
            FROM RankedPuzzles rp2
            WHERE rp2.""GuildId"" = rp1.""GuildId""
              AND rp2.""GuessCount"" > 0
              AND rp2.""SequenceNumber"" > COALESCE(
                  (SELECT MAX(""SequenceNumber"")
                   FROM RankedPuzzles rp3
                   WHERE rp3.""GuildId"" = rp1.""GuildId""
                     AND rp3.""GuessCount"" = 0
                     AND rp3.rn > 1),
                  0 
              )
        ) as ""CalculatedStreak""
    FROM (SELECT DISTINCT ""GuildId"" FROM RankedPuzzles) rp1
)
UPDATE ""Guilds""
SET ""DailyStreak"" = sc.""CalculatedStreak""
FROM StreakCalculations sc
WHERE ""Guilds"".""Id"" = sc.""GuildId"";
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DailyStreak",
                table: "Guilds");
        }
    }
}
