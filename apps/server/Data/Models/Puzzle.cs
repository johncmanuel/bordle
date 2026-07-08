
namespace Bordle.Server.Data.Models
{
    public class Puzzle
    {
        public int Id { get; set; }

        public long GuildId { get; set; }
        public Guild Guild { get; set; } = null!;

        public int? SubmissionId { get; set; }
        public WordSubmission? Submission { get; set; }

        public string? FallbackWord { get; set; }
        public List<string>? GeneratedHints { get; set; }

        public int SequenceNumber { get; set; }

        public DateTime PublishedAt { get; set; }
        public DateTime ClosedAt { get; set; }

        public ICollection<Guess> Guesses { get; set; } = [];
    }
}
