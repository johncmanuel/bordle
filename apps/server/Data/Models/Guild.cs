using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Bordle.Server.Data.Models
{
    public class Guild
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.None)]
        public long Id { get; set; }

        public ICollection<WordSubmission> WordSubmissions { get; set; } = [];
        public ICollection<Puzzle> Puzzles { get; set; } = [];
        public ICollection<Guess> Guesses { get; set; } = [];
    }
}
