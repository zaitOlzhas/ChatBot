namespace ChatBotAPI.Domain;

public class Session
{
    public int Id { get; set; }

    public long UserId { get; set; }
    public User? User { get; set; }

    public string? CurrentStep { get; set; }

    public string? ContextJson { get; set; } // stored as JSON

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
