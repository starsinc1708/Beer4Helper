using System.ComponentModel.DataAnnotations;

namespace Beer4Helper.ReactionCounter.Models
{
    public class Reaction
    {
        [Key]
        public int Id { get; set; }

        public long ChatId { get; set; }
        public long UserId { get; set; }
        public string? Emoji { get; set; }
        public long MessageId { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime LastUpdated { get; set; } = DateTime.UtcNow;
        
        public virtual UserStats? UserStats { get; set; }
    }
}