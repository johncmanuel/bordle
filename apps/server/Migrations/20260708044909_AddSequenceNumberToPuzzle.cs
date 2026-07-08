using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace server.Migrations
{
    /// <inheritdoc />
    public partial class AddSequenceNumberToPuzzle : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "SequenceNumber",
                table: "Puzzles",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            // Backfill existing puzzles with sequential numbers per guild
            migrationBuilder.Sql("""
                WITH RankedPuzzles AS (
                    SELECT "Id", ROW_NUMBER() OVER (PARTITION BY "GuildId" ORDER BY "PublishedAt") AS SeqNum
                    FROM "Puzzles"
                )
                UPDATE "Puzzles"
                SET "SequenceNumber" = r.SeqNum
                FROM RankedPuzzles r
                WHERE "Puzzles"."Id" = r."Id";
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "SequenceNumber",
                table: "Puzzles");
        }
    }
}
