using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using Microsoft.Extensions.DependencyInjection;
using Serilog;
using SteamBot.Config;

namespace SteamBot.Discord;

public class BotClient
{
    private readonly DiscordSocketClient _client;
    private readonly InteractionService _interactions;
    private readonly IServiceProvider _services;
    private readonly BotConfig _config;

    public BotClient(IServiceProvider services, BotConfig config)
    {
        _services = services;
        _config = config;

        _client = services.GetRequiredService<DiscordSocketClient>();
        _interactions = services.GetRequiredService<InteractionService>();
    }

    public async Task StartAsync()
    {
        _client.Log += OnLog;
        _interactions.Log += OnLog;

        _client.Ready += OnReady;
        _client.InteractionCreated += OnInteractionCreated;

        // Load all interaction modules from this assembly
        await _interactions.AddModulesAsync(typeof(BotClient).Assembly, _services);

        await _client.LoginAsync(TokenType.Bot, _config.DiscordToken);
        await _client.StartAsync();

        await _client.SetActivityAsync(new Game("Steam stats | /steam profile"));
    }

    private async Task OnReady()
    {
        Log.Information("Logged in as {Username}#{Discriminator}", _client.CurrentUser.Username, _client.CurrentUser.Discriminator);

        if (_config.TestGuildId.HasValue)
        {
            // Register commands to a single guild instantly (for development)
            await _interactions.RegisterCommandsToGuildAsync(_config.TestGuildId.Value, deleteMissing: true);
            Log.Information("Slash commands registered to test guild {GuildId}", _config.TestGuildId.Value);
        }
        else
        {
            // Register globally (takes up to 1 hour to propagate)
            await _interactions.RegisterCommandsGloballyAsync(deleteMissing: true);
            Log.Information("Slash commands registered globally");
        }
    }

    private async Task OnInteractionCreated(SocketInteraction interaction)
    {
        try
        {
            var ctx = new SocketInteractionContext(_client, interaction);
            await _interactions.ExecuteCommandAsync(ctx, _services);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Unhandled exception in interaction {InteractionId}", interaction.Id);

            if (interaction.Type == InteractionType.ApplicationCommand)
            {
                await interaction.GetOriginalResponseAsync()
                    .ContinueWith(async msg => await msg.Result.DeleteAsync());
            }
        }
    }

    private static Task OnLog(LogMessage msg)
    {
        var level = msg.Severity switch
        {
            LogSeverity.Critical => Serilog.Events.LogEventLevel.Fatal,
            LogSeverity.Error    => Serilog.Events.LogEventLevel.Error,
            LogSeverity.Warning  => Serilog.Events.LogEventLevel.Warning,
            LogSeverity.Info     => Serilog.Events.LogEventLevel.Information,
            LogSeverity.Verbose  => Serilog.Events.LogEventLevel.Verbose,
            LogSeverity.Debug    => Serilog.Events.LogEventLevel.Debug,
            _                    => Serilog.Events.LogEventLevel.Information
        };

        if (msg.Exception is null)
            Log.Write(level, "[Discord] {Message}", msg.Message);
        else
            Log.Write(level, msg.Exception, "[Discord] {Message}", msg.Message);

        return Task.CompletedTask;
    }
}
