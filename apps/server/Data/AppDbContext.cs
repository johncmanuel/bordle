using Microsoft.EntityFrameworkCore;
using Bordle.Server.Data.Models;

namespace Bordle.Server.Data
{
    public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
    {
        public DbSet<User> Users { get; set; }
        public DbSet<Guild> Guilds { get; set; }
        public DbSet<WordSubmission> WordSubmissions { get; set; }
        public DbSet<Puzzle> Puzzles { get; set; }
        public DbSet<Guess> Guesses { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<WordSubmission>(entity =>
            {
                // Enforce max string length at DB layer for varchar array elements
                entity.Property(e => e.Hints).HasColumnType("varchar(25)[]");
                
                // Enforce max 3 hints
                entity.ToTable(t => t.HasCheckConstraint("CK_Hints_Count", "array_length(\"Hints\", 1) <= 3"));
                
                // Enforce exactly 5 chars for the word
                entity.ToTable(t => t.HasCheckConstraint("CK_Word_Length", "char_length(\"Word\") = 5"));
            });

            modelBuilder.Entity<Puzzle>(entity =>
            {
                entity.Property(e => e.GeneratedHints).HasColumnType("varchar(25)[]");
                
                // Enforce exactly 5 chars for the fallback word
                entity.ToTable(t => t.HasCheckConstraint("CK_FallbackWord_Length", "\"FallbackWord\" IS NULL OR char_length(\"FallbackWord\") = 5"));
            });

            modelBuilder.Entity<Guess>(entity =>
            {
                // Ensure that attempts are between 1 and 6
                entity.ToTable(t => t.HasCheckConstraint("CK_AttemptNumber", "\"AttemptNumber\" BETWEEN 1 AND 6"));
                
                // Enforce exactly 5 chars for the guess
                entity.ToTable(t => t.HasCheckConstraint("CK_Guess_Length", "char_length(\"Word\") = 5"));
            });
        }
    }
}
