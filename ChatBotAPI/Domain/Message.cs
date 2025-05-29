namespace ChatBotAPI.Domain;

public class Message
{
    public long Id { get; set; }
    public int MessageId { get; set; }

    public long ChatId { get; set; }
    public Chat? Chat { get; set; }

    public long FromUserId { get; set; }
    public User? FromUser { get; set; }

    public string? Text { get; set; }
    public DateTime SentAt { get; set; } = DateTime.UtcNow;
}
