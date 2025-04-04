namespace Beer4Helper.ReactionCounter.DTOs;

public class Requests
{
    public abstract class CreateTopMessageRequest
    {
        public long ChatId { get; set; }
    }


    public abstract class ChatMemberRequest {
        public long ChatId { get; set; }
    }

    public abstract class DeleteChatMessageRequest
    {
        public long ChatId { get; set; }
        public long MessageId { get; set; }
    }

    public abstract class EditMessageRequest
    {
        public long ChatId { get; set; }
        public long MessageId { get; set; }
        public required string Text { get; set; }
    }

    public abstract class TopStatsRequest
    {
        public long ChatId { get; set; }
        public long SendToChatId { get; set; }
    }

    public abstract class CustomTopStatsRequest
    {
        public long ChatId { get; set; }
        public long SendToChatId { get; set; }
        public string Period { get; set; } = "1m";
        public int TopCount { get; set; } = 10;
    
        public required string TopType { get; set; }
    }
}