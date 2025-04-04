using System.ComponentModel.DataAnnotations;

namespace Beer4Helper.ReactionCounter.Models;

public class TopMessage
{
    [Key]
    public int Id { get; init; }
    public long ChatId { get; init; }
    public int MessageId { get; set; }
    public string Text { get; set; } = "Скоро тут появится статистика \nЗАКРЕПИ МЕНЯ!";
    public DateTime EditedAt { get; set; }
    
}