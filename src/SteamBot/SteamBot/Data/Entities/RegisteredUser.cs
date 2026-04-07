namespace SteamBot.Data.Entities;

public class RegisteredUser
{
    public int Id { get; set; }
    public ulong DiscordUserId { get; set; }
    public string SteamId { get; set; } = string.Empty;
    public DateTime RegisteredAt { get; set; } = DateTime.UtcNow;
}
