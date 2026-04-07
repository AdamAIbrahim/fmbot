using Microsoft.EntityFrameworkCore;
using SteamBot.Data.Entities;

namespace SteamBot.Data;

public class BotDbContext : DbContext
{
    public DbSet<RegisteredUser> RegisteredUsers { get; set; }
    public DbSet<Friend> Friends { get; set; }

    public BotDbContext(DbContextOptions<BotDbContext> options) : base(options) { }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<RegisteredUser>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.DiscordUserId).IsUnique();
            entity.HasIndex(e => e.SteamId);
            entity.Property(e => e.DiscordUserId).IsRequired();
            entity.Property(e => e.SteamId).IsRequired();
        });

        modelBuilder.Entity<Friend>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => new { e.DiscordUserId, e.FriendDiscordUserId }).IsUnique();
            entity.Property(e => e.DiscordUserId).IsRequired();
            entity.Property(e => e.FriendDiscordUserId).IsRequired();
        });
    }
}
