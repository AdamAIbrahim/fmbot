using Microsoft.EntityFrameworkCore;
using SteamBot.Data.Entities;

namespace SteamBot.Data;

public class UserRepository
{
    private readonly BotDbContext _db;

    public UserRepository(BotDbContext db) => _db = db;

    public async Task<RegisteredUser?> GetByDiscordIdAsync(ulong discordUserId) =>
        await _db.RegisteredUsers.FirstOrDefaultAsync(u => u.DiscordUserId == discordUserId);

    public async Task<RegisteredUser?> GetBySteamIdAsync(string steamId) =>
        await _db.RegisteredUsers.FirstOrDefaultAsync(u => u.SteamId == steamId);

    public async Task UpsertAsync(ulong discordUserId, string steamId)
    {
        var existing = await GetByDiscordIdAsync(discordUserId);
        if (existing is not null)
        {
            existing.SteamId = steamId;
            existing.RegisteredAt = DateTime.UtcNow;
        }
        else
        {
            _db.RegisteredUsers.Add(new RegisteredUser
            {
                DiscordUserId = discordUserId,
                SteamId = steamId
            });
        }

        await _db.SaveChangesAsync();
    }

    public async Task<bool> DeleteAsync(ulong discordUserId)
    {
        var user = await GetByDiscordIdAsync(discordUserId);
        if (user is null) return false;
        _db.RegisteredUsers.Remove(user);
        await _db.SaveChangesAsync();
        return true;
    }

    public async Task<List<RegisteredUser>> GetAllAsync() =>
        await _db.RegisteredUsers.ToListAsync();

    public async Task<List<Friend>> GetFriendsAsync(ulong discordUserId) =>
        await _db.Friends.Where(f => f.DiscordUserId == discordUserId).ToListAsync();

    public async Task<bool> AddFriendAsync(ulong discordUserId, ulong friendDiscordUserId)
    {
        var exists = await _db.Friends.AnyAsync(
            f => f.DiscordUserId == discordUserId && f.FriendDiscordUserId == friendDiscordUserId);
        if (exists) return false;

        _db.Friends.Add(new Friend
        {
            DiscordUserId = discordUserId,
            FriendDiscordUserId = friendDiscordUserId
        });
        await _db.SaveChangesAsync();
        return true;
    }

    public async Task<bool> RemoveFriendAsync(ulong discordUserId, ulong friendDiscordUserId)
    {
        var entry = await _db.Friends.FirstOrDefaultAsync(
            f => f.DiscordUserId == discordUserId && f.FriendDiscordUserId == friendDiscordUserId);
        if (entry is null) return false;

        _db.Friends.Remove(entry);
        await _db.SaveChangesAsync();
        return true;
    }
}
