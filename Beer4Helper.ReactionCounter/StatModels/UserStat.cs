namespace Beer4Helper.ReactionCounter.StatModels;

public class UserStat
{
    public required string Username { get; set; } = "@unknown";
    public int TotalReactions { get; set; }
    public int TotalPhotos { get; set; }
}