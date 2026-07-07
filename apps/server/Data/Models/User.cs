using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Bordle.Server.Data.Models
{
    public class User
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.None)]
        public long Id { get; set; }
        public string? Username { get; set; }

        // the avatar hash returned by Discord, which can be used to construct the avatar URL with discord's CDN
        public string? Avatar { get; set; }

        public ICollection<WordSubmission> WordSubmissions { get; set; } = [];
        public ICollection<Guess> Guesses { get; set; } = [];
    }
}
