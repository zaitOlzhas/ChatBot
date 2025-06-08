using System.Text;
using System.Text.Json;
using ChatBotAPI.ConfigurationModels;
using ChatBotAPI.DataBase;
using ChatBotAPI.Services.LLM;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Telegram.Bot;
using Telegram.Bot.Polling;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

namespace ChatBotAPI.Services.Telegram;

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
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<ChatBotDbContext>();

            if (update.Message is { } message && !string.IsNullOrEmpty(message.Text) && message.From is { } user)
            {
                _logger.LogInformation("Received message: {Message}", JsonSerializer.Serialize(message));
                await HandleUser(context, user);
                if (message.Text.StartsWith("/"))
                {
                    _logger.LogInformation("Handling command: {Command}", message.Text);
                    await HandleCommand(botClient, scope, message, context, ct);
                }
                else
                {
                    _logger.LogInformation("Handling message: {MessageText}", message.Text);
                    await HandleMessage(context, message);
                    await botClient.SendMessage(
                        chatId: message.Chat.Id,
                        text: $"Для использова ИИ пожалуйста начни сообщения с символа '/'",
                        cancellationToken: ct
                    );
                }
            }
        }
        catch (OperationCanceledException ex)
        {
            _logger.LogWarning(ex, "Operation was canceled: {Update}", JsonSerializer.Serialize(update));
            await botClient.SendMessage(
                chatId: update.Message.Chat.Id,
                text: $"Операция отменена",
                cancellationToken: ct
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling update: {Update}", JsonSerializer.Serialize(update));
            await HandleErrorAsync(botClient, ex, ct);
        }
    }

    private async Task HandleCommand(ITelegramBotClient botClient, IServiceScope scope, Message message, ChatBotDbContext context, CancellationToken ct)
    {
        var command = message.Text.Split(' ')[0].ToLower();
        var ollama = scope.ServiceProvider.GetRequiredService<OllamaServices>();
        var models = await ollama.ListModelsAsync(ct);
        if (command == "/ollama" && models.Length == 0)
        {
            _logger.LogInformation($"Ollama command without models, pulling default model 'mistral'.");
            await ollama.PullModelAsync("mistral", ct);
            await botClient.SendMessage(
                chatId: message.Chat.Id,
                text: $"Pulling model mistral, please wait...",
                cancellationToken: ct
            );
            return;
        }
        switch (command)
        {
            case "/":
                if (message.Text.Length <= command.Length)
                {
                    await botClient.SendMessage(
                        chatId: message.Chat.Id,
                        text: "Please provide a prompt after the command.",
                        cancellationToken: ct
                    );
                    return;
                }
                var prompt = message.Text.Substring(command.Length).Trim();
                if (!string.IsNullOrEmpty(prompt))
                {
                    await botClient.SendMessage(
                        chatId: message.Chat.Id,
                        text: "Идет обработка запроса, пожалуйста, подождите...",
                        cancellationToken: ct
                    );
                    var llmResponse = await ollama.UserPromptAsync("mistral:latest", prompt, ct);
                    await botClient.SendMessage(
                        chatId: message.Chat.Id,
                        text: EscapeTelegramMarkdown(llmResponse.Message.Content),
                        parseMode: ParseMode.MarkdownV2,
                        cancellationToken: ct
                    );
                }
                return;
            case "/status":
                var chats = await context.Chats.CountAsync(ct);
                var users = await context.Users.CountAsync(ct);
                await botClient.SendMessage(
                    chatId: message.Chat.Id,
                    text: $"Total chats: {chats}, Total users: {users}",
                    cancellationToken: ct
                );
                return;
            default:
                await botClient.SendMessage(
                    chatId: message.Chat.Id,
                    text: $"Unknown command: {command}. Available commands: /ollama",
                    cancellationToken: ct
                );
                return;
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
    public static string EscapeTelegramMarkdown(string text)
    {
        var charsToEscape = new[] { '_', '*', '[', ']', '(', ')', '~', '`', '>', '#', '+', '-', '=', '|', '{', '}', '.', '!' };
        var sb = new StringBuilder();

        foreach (var c in text)
        {
            if (charsToEscape.Contains(c))
                sb.Append('\\');
            sb.Append(c);
        }

        return sb.ToString();
    }
}