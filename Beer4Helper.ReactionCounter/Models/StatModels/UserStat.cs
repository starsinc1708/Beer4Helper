namespace Beer4Helper.ReactionCounter.Models.StatModels;

public class UserStat
{
    public required string Username { get; init; } = "@unknown";
    public int TotalReactions { get; init; }
    public int TotalPhotos { get; init; }
}