namespace SteamBot.Config;

public class BotConfig
{
    public string DiscordToken { get; set; } = string.Empty;
    public ulong? TestGuildId { get; set; }
    public string SteamApiKey { get; set; } = string.Empty;
    public string DatabasePath { get; set; } = "steambot.db";
}
