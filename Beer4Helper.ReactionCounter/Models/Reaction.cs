using System.ComponentModel.DataAnnotations;

namespace Beer4Helper.ReactionCounter.Models
{
    public class Reaction
    {
        [Key]
        public int Id { get; init; }

        public long ChatId { get; init; }
        public long UserId { get; init; }
        public string? Emoji { get; init; }
        public long MessageId { get; init; }
        public DateTime CreatedAt { get; init; } = DateTime.UtcNow;
        public DateTime LastUpdated { get; init; } = DateTime.UtcNow;
    }
}