using System.Text.Json;
using System.Text.Json.Serialization;
using ChatBotAPI.ConfigurationModels;
using ChatBotAPI.DataBase;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Telegram.Bot;
using Telegram.Bot.Polling;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

namespace ChatBotAPI.Services;

public class TelegramBotService : BackgroundService
{
    private readonly ILogger<TelegramBotService> _logger;
    private readonly TelegramBotClient _botClient;
    private readonly ReceiverOptions _receiverOptions;
    private readonly IServiceScopeFactory _scopeFactory;
    
    public TelegramBotService(IOptions<TelegramConfig> telegramConfigs,ILogger<TelegramBotService> logger, IServiceScopeFactory scopeFactory)
    {
        _logger = logger;
        _botClient = new TelegramBotClient(telegramConfigs.Value.Token);
        _scopeFactory = scopeFactory;
        
        _receiverOptions = new ReceiverOptions
        {
            AllowedUpdates = []
        };
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _botClient.StartReceiving(
            HandleUpdateAsync,
            HandleErrorAsync,
            _receiverOptions,
            cancellationToken: stoppingToken
        );

        var me = await _botClient.GetMe();
        _logger.LogInformation("Telegram Bot started: {Username}", JsonSerializer.Serialize(me));
    }
    
    private async Task HandleUpdateAsync(ITelegramBotClient botClient, Update update, CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ChatBotDbContext>();
        
        if (update.Message is { } message && !string.IsNullOrEmpty(message.Text) && message.From is { } user)
        {
            await HandleUser(context, user);
            if (message.Text.StartsWith("/"))
            {
                //await HandleCommand(message);
            }
            else
            {
                await HandleMessage(context, message);

                await botClient.SendMessage(
                    chatId: message.Chat.Id,
                    text: $"Hello! You said: {message.Text}",
                    cancellationToken: ct
                );
            }
        }
    }

    private async Task HandleMessage(ChatBotDbContext context, Message message)
    {
        var existingChat = await context.Chats.FirstOrDefaultAsync(c => c.ChatId == message.Chat.Id);
        if (existingChat == null)
        {
            existingChat = new Domain.Chat
            {
                ChatId = message.Chat.Id,
                Type = message.Chat.Type.ToString(),
                Title = message.Chat.Title ?? "Untitled",
                CreatedAt = DateTime.UtcNow
            };
            await context.Chats.AddAsync(existingChat);
            await context.SaveChangesAsync();
        }

        var fromUserId = await HandleUser(context, message.From);

        await context.Messages.AddAsync(new Domain.Message
        {
            MessageId = message.MessageId,
            ChatId = existingChat.Id,
            FromUserId = fromUserId,
            Text = message.Text,
            SentAt = message.Date
        });
        await context.SaveChangesAsync();
    }

    private async Task<long> HandleUser(ChatBotDbContext context, User? user)
    {
        var existingUser = await context.Users.FirstOrDefaultAsync(u => u.TelegramId == user.Id);

        if (existingUser is null)
        {
            var newUser = await context.Users.AddAsync(new Domain.User
            {
                TelegramId = user.Id,
                Username = user.Username,
                FirstName = user.FirstName,
                LastName = user.LastName,
                LanguageCode = user.LanguageCode,
                IsBot = user.IsBot,
                CreatedAt = DateTime.UtcNow,
                LastActiveAt = DateTime.UtcNow,
                IsPremium = user.IsPremium,
            });
            await context.SaveChangesAsync();
            return newUser.Entity.Id;
        }
        
        existingUser.LastActiveAt = DateTime.UtcNow;
        await context.SaveChangesAsync();
        
        return existingUser.Id;
    }

    private Task HandleErrorAsync(ITelegramBotClient botClient, Exception exception, CancellationToken ct)
    {
        _logger.LogError(exception, "Telegram bot error");
        return Task.CompletedTask;
    }
}