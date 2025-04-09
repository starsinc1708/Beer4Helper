using System.ComponentModel.DataAnnotations;

namespace Beer4Helper.BeerEventManager.Models;

public class Poll
{
    [Key]
    public long Id { get; set; }
    
    public long ChatId { get; set; }
    public long MessageId { get; set; }
    
    public string? MessageText { get; set; }
    public int TotalVotes { get; set; }
    
    public DateTime CreatedAt { get; set; }
    public TimeSpan? Duration { get; set; }
    public DateTime ClosedAt { get; set; }
    
    public List<PollOption>? Options { get; set; }
}