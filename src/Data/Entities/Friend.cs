namespace SteamBot.Data.Entities;

public class Friend
{
    public int Id { get; set; }
    public ulong DiscordUserId { get; set; }        // the user who added the friend
    public ulong FriendDiscordUserId { get; set; }  // the friend who was added
    public DateTime AddedAt { get; set; } = DateTime.UtcNow;
}
