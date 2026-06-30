using System.ComponentModel.DataAnnotations;

namespace Bordle.Server.Data.Models
{
    public class WordSubmission
    {
        public int Id { get; set; }

        public long GuildId { get; set; }
        public Guild Guild { get; set; } = null!;

        public long UserId { get; set; }
        public User User { get; set; } = null!;

        [Required]
        public string Word { get; set; } = null!;

        public List<string> Hints { get; set; } = [];

        public DateTime SubmittedAt { get; set; }
        public bool IsUsed { get; set; } = false;
    }
}
