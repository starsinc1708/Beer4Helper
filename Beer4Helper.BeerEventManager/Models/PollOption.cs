using System.ComponentModel.DataAnnotations;

namespace Beer4Helper.BeerEventManager.Models;

public class PollOption
{
    [Key]
    public long Id { get; set; }
    
    public long PollId { get; set; }
    public Poll? Poll { get; set; }
    
    public string? Text { get; set; }
    public int VotesCount { get; set; }
    
    public List<UserVote>? UserVotes { get; set; }
}