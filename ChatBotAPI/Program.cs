using ChatBotAPI.ConfigurationModels;
using ChatBotAPI.DataBase;
using ChatBotAPI.Services;
using Microsoft.EntityFrameworkCore;

namespace ChatBotAPI;

public class Program
{
    public static void Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);
        if (builder.Environment.IsDevelopment())
        {
            DotNetEnv.Env.Load();
        }
        Console.WriteLine("ConnectionString: " + builder.Configuration.GetConnectionString("ChatBotDb"));
        Console.WriteLine("Telegram Token: " + builder.Configuration["Telegram:Token"]);
        builder.Services.AddDbContext<ChatBotDbContext>(options =>
            options.UseNpgsql(builder.Configuration.GetConnectionString("ChatBotDb")));
        
        builder.Services.Configure<TelegramConfig>(builder.Configuration.GetSection("Telegram"));
        
        builder.Services.AddHostedService<TelegramBotService>();
        builder.Services.AddHealthChecks()
            .AddNpgSql(builder.Configuration.GetConnectionString("ChatBotDb"), name: "postgresql");

        builder.Services.AddOpenApi();

        var app = builder.Build();

        // Configure the HTTP request pipeline.
        if (app.Environment.IsDevelopment())
        {
            app.MapOpenApi();
        }

        app.UseHttpsRedirection();
        
        using (var scope = app.Services.CreateScope())
        {
            var services = scope.ServiceProvider;
            var context = services.GetRequiredService<ChatBotDbContext>();
            context.Database.Migrate(); // Applies pending migrations
        }
        
        app.MapHealthChecks("/health");
        app.Run();
    }
}