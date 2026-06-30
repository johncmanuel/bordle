using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace server.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Guilds",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Guilds", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Users",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Users", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "WordSubmissions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    GuildId = table.Column<long>(type: "bigint", nullable: false),
                    UserId = table.Column<long>(type: "bigint", nullable: false),
                    Word = table.Column<string>(type: "text", nullable: false),
                    Hints = table.Column<List<string>>(type: "varchar(25)[]", nullable: false),
                    SubmittedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    IsUsed = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WordSubmissions", x => x.Id);
                    table.CheckConstraint("CK_Hints_Count", "array_length(\"Hints\", 1) <= 3");
                    table.CheckConstraint("CK_Word_Length", "char_length(\"Word\") = 5");
                    table.ForeignKey(
                        name: "FK_WordSubmissions_Guilds_GuildId",
                        column: x => x.GuildId,
                        principalTable: "Guilds",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_WordSubmissions_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Puzzles",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    GuildId = table.Column<long>(type: "bigint", nullable: false),
                    SubmissionId = table.Column<int>(type: "integer", nullable: true),
                    FallbackWord = table.Column<string>(type: "text", nullable: true),
                    GeneratedHints = table.Column<List<string>>(type: "varchar(25)[]", nullable: true),
                    PublishedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ClosedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Puzzles", x => x.Id);
                    table.CheckConstraint("CK_FallbackWord_Length", "\"FallbackWord\" IS NULL OR char_length(\"FallbackWord\") = 5");
                    table.ForeignKey(
                        name: "FK_Puzzles_Guilds_GuildId",
                        column: x => x.GuildId,
                        principalTable: "Guilds",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Puzzles_WordSubmissions_SubmissionId",
                        column: x => x.SubmissionId,
                        principalTable: "WordSubmissions",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "Guesses",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    PuzzleId = table.Column<int>(type: "integer", nullable: false),
                    UserId = table.Column<long>(type: "bigint", nullable: false),
                    GuildId = table.Column<long>(type: "bigint", nullable: false),
                    AttemptNumber = table.Column<short>(type: "smallint", nullable: false),
                    Word = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Guesses", x => x.Id);
                    table.CheckConstraint("CK_AttemptNumber", "\"AttemptNumber\" BETWEEN 1 AND 6");
                    table.CheckConstraint("CK_Guess_Length", "char_length(\"Word\") = 5");
                    table.ForeignKey(
                        name: "FK_Guesses_Guilds_GuildId",
                        column: x => x.GuildId,
                        principalTable: "Guilds",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Guesses_Puzzles_PuzzleId",
                        column: x => x.PuzzleId,
                        principalTable: "Puzzles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Guesses_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Guesses_GuildId",
                table: "Guesses",
                column: "GuildId");

            migrationBuilder.CreateIndex(
                name: "IX_Guesses_PuzzleId",
                table: "Guesses",
                column: "PuzzleId");

            migrationBuilder.CreateIndex(
                name: "IX_Guesses_UserId",
                table: "Guesses",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_Puzzles_GuildId",
                table: "Puzzles",
                column: "GuildId");

            migrationBuilder.CreateIndex(
                name: "IX_Puzzles_SubmissionId",
                table: "Puzzles",
                column: "SubmissionId");

            migrationBuilder.CreateIndex(
                name: "IX_WordSubmissions_GuildId",
                table: "WordSubmissions",
                column: "GuildId");

            migrationBuilder.CreateIndex(
                name: "IX_WordSubmissions_UserId",
                table: "WordSubmissions",
                column: "UserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Guesses");

            migrationBuilder.DropTable(
                name: "Puzzles");

            migrationBuilder.DropTable(
                name: "WordSubmissions");

            migrationBuilder.DropTable(
                name: "Guilds");

            migrationBuilder.DropTable(
                name: "Users");
        }
    }
}
