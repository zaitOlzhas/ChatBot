namespace ChatBotAPI.Domain;

public class CommandLog
{
    public int Id { get; set; }

    public long UserId { get; set; }
    public User? User { get; set; }

    public string Command { get; set; } = default!;
    public string? Arguments { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
