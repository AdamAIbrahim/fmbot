using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Serilog;
using SteamBot.Config;
using SteamBot.Data;
using SteamBot.Discord;
using SteamBot.Steam;

// Configure Serilog early so any startup errors are captured
Log.Logger = new LoggerConfiguration()
    .WriteTo.Console(outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}")
    .MinimumLevel.Information()
    .CreateLogger();

try
{
    // Load config
    var configuration = new ConfigurationBuilder()
        .SetBasePath(Directory.GetCurrentDirectory())
        .AddJsonFile("appsettings.json", optional: false, reloadOnChange: false)
        .AddEnvironmentVariables("STEAMBOT_")
        .Build();

    var config = configuration.GetSection("Bot").Get<BotConfig>()
        ?? throw new InvalidOperationException("Missing 'Bot' section in appsettings.json");

    if (string.IsNullOrWhiteSpace(config.DiscordToken) || config.DiscordToken == "YOUR_DISCORD_BOT_TOKEN")
        throw new InvalidOperationException("Set Bot:DiscordToken in appsettings.json");

    if (string.IsNullOrWhiteSpace(config.SteamApiKey) || config.SteamApiKey == "YOUR_STEAM_API_KEY")
        throw new InvalidOperationException("Set Bot:SteamApiKey in appsettings.json");

    // Build DI container
    var services = new ServiceCollection();

    // Discord.Net
    var socketConfig = new DiscordSocketConfig
    {
        GatewayIntents = GatewayIntents.Guilds,
        LogLevel = LogSeverity.Info,
        MessageCacheSize = 0
    };
    services.AddSingleton(socketConfig);
    services.AddSingleton<DiscordSocketClient>();
    services.AddSingleton(provider =>
        new InteractionService(
            provider.GetRequiredService<DiscordSocketClient>(),
            new InteractionServiceConfig { LogLevel = LogSeverity.Info }));

    // Steam API
    services.AddSingleton(_ => new SteamApiClient(new HttpClient { Timeout = TimeSpan.FromSeconds(15) }, config.SteamApiKey));

    // Database (SQLite)
    services.AddDbContext<BotDbContext>(opt =>
        opt.UseSqlite($"Data Source={config.DatabasePath}"));
    services.AddScoped<UserRepository>();

    // Bot
    services.AddSingleton(config);
    services.AddSingleton<BotClient>();

    var provider = services.BuildServiceProvider();

    // Ensure database schema exists
    await using (var scope = provider.CreateAsyncScope())
    {
        var db = scope.ServiceProvider.GetRequiredService<BotDbContext>();
        await db.Database.EnsureCreatedAsync();
        Log.Information("Database ready at {Path}", config.DatabasePath);
    }

    // Start bot
    var bot = provider.GetRequiredService<BotClient>();
    await bot.StartAsync();

    Log.Information("SteamBot is running. Press Ctrl+C to stop.");
    await Task.Delay(Timeout.Infinite);
}
catch (Exception ex)
{
    Log.Fatal(ex, "SteamBot terminated unexpectedly");
}
finally
{
    await Log.CloseAndFlushAsync();
}
