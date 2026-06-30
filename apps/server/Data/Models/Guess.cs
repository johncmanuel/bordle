using System.ComponentModel.DataAnnotations;

namespace Bordle.Server.Data.Models
{
    public class Guess
    {
        public int Id { get; set; }

        public int PuzzleId { get; set; }
        public Puzzle Puzzle { get; set; } = null!;

        public long UserId { get; set; }
        public User User { get; set; } = null!;

        public long GuildId { get; set; }
        public Guild Guild { get; set; } = null!;

        public short AttemptNumber { get; set; }

        [Required]
        public string Word { get; set; } = null!;

        public DateTime CreatedAt { get; set; }
    }
}
