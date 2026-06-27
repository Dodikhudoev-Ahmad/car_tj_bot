using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Serilog;
using Telegram.Bot;
using Telegram.Bot.Polling;
using Telegram.Bot.Types.Enums;
using TelegramCarsBot.Data;
using TelegramCarsBot.Handlers;
using TelegramCarsBot.Services;

Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateLogger();

// Очищаем переменную PORT, чтобы она не ломала драйвер PostgreSQL
Environment.SetEnvironmentVariable("PORT", null);

// ─── Host ─────────────────────────────────────────────────────────────────────
var host = Host.CreateDefaultBuilder(args)
    .UseSerilog()
    .ConfigureAppConfiguration(config =>
    {
        config.AddJsonFile("appsettings.json", optional: false);
        config.AddEnvironmentVariables(); 
    })
    .ConfigureServices((ctx, services) =>
    {
        var token = ctx.Configuration["BotToken"]
            ?? throw new Exception("BotToken не задан в appsettings.json");

        var rawConnStr = Environment.GetEnvironmentVariable("DATABASE_URL")
                         ?? ctx.Configuration.GetConnectionString("Default")
                         ?? throw new Exception("ConnectionString не задан");

        string connStr;
        if (rawConnStr.StartsWith("postgres://") || rawConnStr.StartsWith("postgresql://"))
        {
            var uri = new Uri(rawConnStr);
            var userInfo = uri.UserInfo.Split(':');
            
            // Фикс порта: если в URI порт не указан, uri.Port вернет -1. 
            // В таком случае принудительно ставим стандартный порт PostgreSQL (5432)
            int port = uri.Port == -1 ? 5432 : uri.Port;

            connStr = $"Host={uri.Host};Port={port};Database={uri.AbsolutePath.TrimStart('/')};Username={userInfo[0]};Password={userInfo[1]};SSL Mode=Require;Trust Server Certificate=true";
        }
        else
        {
            connStr = rawConnStr;
        }

        // Telegram Bot
        services.AddSingleton<ITelegramBotClient>(new TelegramBotClient(token));

        // EF Core + PostgreSQL
        services.AddDbContext<AppDbContext>(opt =>
            opt.UseNpgsql(connStr));

        // Services & Handlers
        services.AddScoped<CarService>();
        services.AddScoped<UpdateHandler>();

        // Hosted service
        services.AddHostedService<BotHostedService>();
    })
    .Build();

// ─── Auto-migrate ─────────────────────────────────────────────────────────────
using (var scope = host.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await db.Database.MigrateAsync();
    Log.Information("✅ БД готова");
}

await host.RunAsync();

// ─── BotHostedService ─────────────────────────────────────────────────────────
public class BotHostedService(ITelegramBotClient bot, IServiceProvider sp, ILogger<BotHostedService> logger)
    : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("🤖 Бот запущен");

        var options = new ReceiverOptions
        {
            AllowedUpdates = [UpdateType.Message]
        };

        bot.StartReceiving(
            updateHandler: HandleUpdateAsync,
            errorHandler: HandleErrorAsync,
            receiverOptions: options,
            cancellationToken: stoppingToken
        );

        await Task.Delay(Timeout.Infinite, stoppingToken);
    }

    private async Task HandleUpdateAsync(ITelegramBotClient bot, Telegram.Bot.Types.Update update, CancellationToken ct)
    {
        using var scope = sp.CreateScope();
        var handler = scope.ServiceProvider.GetRequiredService<UpdateHandler>();
        try
        {
            await handler.HandleAsync(update, ct);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Ошибка при обработке апдейта");
        }
    }

    private Task HandleErrorAsync(ITelegramBotClient bot, Exception ex, CancellationToken ct)
    {
        logger.LogError(ex, "Ошибка Telegram");
        return Task.CompletedTask;
    }
}
