namespace Beer4Helper.BeerEventManager.Models.Dto;

public class PollDto
{
    public long ChatId { get; set; }
    public string? MessageText { get; set; }
    public List<PollOptionDto>? Options { get; set; }
}