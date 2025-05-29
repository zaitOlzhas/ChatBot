namespace ChatBotAPI.Domain;

public class Chat
{
    public long Id { get; set; }
    public long ChatId { get; set; }
    public string Type { get; set; } = "private"; // private, group, supergroup, channel
    public string? Title { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public IEnumerable<Message>? Messages { get; set; }
}
