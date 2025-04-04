using System.ComponentModel.DataAnnotations;

namespace Beer4Helper.ReactionCounter.Models
{
    public class UserStats
    {
        [Key]
        public required string Id { get; init; }
        public long UserId { get; init; }
        public long ChatId { get; init; }
        public string? Username { get; set; }
        public int TotalReactions { get; set; }
        public int TotalReactionsOnOwnMessages { get; set; } 
        public int TotalReactionsOnOthersMessages { get; set; } 
        public int TotalPhotosUploaded { get; set; }
        public int TotalUniqueMessages { get; set; }
    }
}