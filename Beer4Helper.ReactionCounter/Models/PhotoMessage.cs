using System.ComponentModel.DataAnnotations;

namespace Beer4Helper.ReactionCounter.Models
{
    public class PhotoMessage
    {
        [Key]
        public int Id { get; set; }
        public long ChatId { get; set; }
        public long UserId { get; set; }
        public int MessageId { get; set; }
        public string FileId { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}