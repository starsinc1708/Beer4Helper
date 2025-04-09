using System.ComponentModel.DataAnnotations;

namespace Beer4Helper.BeerEventManager.Models;

public class UserVote
{
    [Key]
    public long Id { get; set; }
    
    public long PollId { get; set; }
    public Poll? Poll { get; set; }
    
    public long UserId { get; set; }
    
    public long PollOptionId { get; set; }
    public PollOption? PollOption { get; set; }
}