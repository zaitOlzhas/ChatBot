using ChatBotAPI.Domain;
using Microsoft.EntityFrameworkCore;

namespace ChatBotAPI.DataBase;

public class ChatBotDbContext: DbContext
{
    public ChatBotDbContext(DbContextOptions<ChatBotDbContext> options): base(options)
    {
        //Database.EnsureCreated();
    }

    public DbSet<User> Users { get; set; }
    public DbSet<Message> Messages { get; set; }
    public DbSet<Chat> Chats { get; set; }
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        MapUser(modelBuilder);
        MapChat(modelBuilder);
        MapMessage(modelBuilder);
    }

    private void MapMessage(ModelBuilder builder)
    {
        builder.Entity<Message>(message =>
        {
            message.HasKey(x => x.Id);
            message.Property(x => x.MessageId)
                .IsRequired();

            message.Property(x => x.ChatId)
                .IsRequired();

            message.Property(x => x.FromUserId)
                .IsRequired();

            message.Property(x => x.Text)
                .HasMaxLength(4096);

            message.Property(x => x.SentAt)
                .IsRequired();
            
            message.HasOne(x => x.FromUser)
                .WithMany()
                .HasForeignKey(x => x.FromUserId)
                .OnDelete(DeleteBehavior.Cascade);
            
            message.HasOne(x=> x.Chat)
                .WithMany(x => x.Messages)
                .HasForeignKey(x => x.ChatId)
                .OnDelete(DeleteBehavior.Cascade);
            
        });
    }

    private void MapUser(ModelBuilder builder)
    {
        builder.Entity<User>(user =>
        {
            user.HasKey(x => x.Id);
            user.Property(x => x.TelegramId)
                .IsRequired();

            user.Property(x => x.Username)
                .HasMaxLength(100);

            user.Property(x => x.FirstName)
                .HasMaxLength(100);

            user.Property(x => x.LastName)
                .HasMaxLength(100);

            user.Property(x => x.LanguageCode)
                .HasMaxLength(50);

            user.Property(x => x.IsBot)
                .IsRequired();
            
            user.Property(x => x.IsPremium)
                .IsRequired();

            user.Property(x => x.CreatedAt)
                .IsRequired();

            user.Property(x => x.LastActiveAt)
                .IsRequired(false);
        });
    }
    private void MapChat(ModelBuilder builder)
    {
        builder.Entity<Chat>(chat =>
        {
            chat.HasKey(x => x.Id);

            chat.Property(x => x.ChatId)
                .IsRequired();

            chat.Property(x => x.Type)
                .IsRequired();

            chat.Property(x => x.Title)
                .HasMaxLength(255);

            chat.Property(x => x.CreatedAt)
                .IsRequired();
        });
    }
}