namespace ChatBotAPI.Domain;
public class User
{
    public long Id { get; set; }
    public long TelegramId { get; set; }
    public string? Username { get; set; }
    public string? FirstName { get; set; }
    public string? LastName { get; set; }
    public string? LanguageCode { get; set; }
    public bool IsBot { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? LastActiveAt { get; set; }

    public bool IsPremium { get; set; } 
    
    //public ICollection<Session>? Sessions { get; set; }
    //public ICollection<CommandLog>? CommandLogs { get; set; }
}