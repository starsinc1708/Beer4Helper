using System.ComponentModel.DataAnnotations;

namespace Beer4Helper.ReactionCounter.Models
{
    public class PhotoMessage
    {
        [Key]
        public int Id { get; init; }
        public long ChatId { get; init; }
        public long UserId { get; init; }
        public int MessageId { get; init; }
        public string FileId { get; init; } = string.Empty;
        public DateTime CreatedAt { get; init; } = DateTime.UtcNow;
    }
}