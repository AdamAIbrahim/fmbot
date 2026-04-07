using System.Text;
using Discord;
using Discord.Interactions;
using SteamBot.Data;
using SteamBot.Steam;

namespace SteamBot.Discord.Modules;

// /server topgames [count] [sort]
// /server recentgames [count]
[Group("server", "Server-wide Steam stats")]
public class ServerModule : InteractionModuleBase<SocketInteractionContext>
{
    private readonly UserRepository _users;
    private readonly SteamApiClient _steam;

    private const uint SteamColor = EmbedHelpers.SteamColor;

    public ServerModule(UserRepository users, SteamApiClient steam)
    {
        _users = users;
        _steam = steam;
    }

    // -----------------------------------------------------------------------
    // /server topgames [count] [sort]
    // -----------------------------------------------------------------------

    [SlashCommand("topgames", "Most popular games across the server by total playtime")]
    public async Task ServerTopGamesAsync(
        [Summary("count", "How many games to show (1–20, default 10)")] int count = 10,
        [Summary("sort", "Rank by player count (default) or combined hours")]
        [Choice("Player Count", "players"), Choice("Total Hours", "hours")]
        string sort = "players")
    {
        await DeferAsync();
        count = Math.Clamp(count, 1, 20);

        var guildRegistered = await GetGuildRegisteredUsersAsync();
        if (guildRegistered.Count == 0)
        {
            await FollowupAsync(embed: NoMembersEmbed());
            return;
        }

        // Fetch every member's game library in parallel
        var libraryTasks = guildRegistered.Select(r => _steam.GetOwnedGamesAsync(r.SteamId));
        var libraries = await Task.WhenAll(libraryTasks);

        // Aggregate by AppId
        var gameStats = new Dictionary<int, (string Name, int Players, long TotalMinutes)>();
        foreach (var lib in libraries)
        {
            if (lib?.Games is null) continue;
            foreach (var game in lib.Games)
            {
                if (gameStats.TryGetValue(game.AppId, out var existing))
                    gameStats[game.AppId] = (existing.Name, existing.Players + 1, existing.TotalMinutes + game.PlaytimeMinutes);
                else
                    gameStats[game.AppId] = (game.Name, 1, game.PlaytimeMinutes);
            }
        }

        if (gameStats.Count == 0)
        {
            await FollowupAsync(embed: new EmbedBuilder()
                .WithColor(SteamColor)
                .WithDescription("No game data found for registered server members. Make sure your Steam libraries are public.")
                .Build());
            return;
        }

        var top = (sort == "hours"
                ? gameStats.Values.OrderByDescending(g => g.TotalMinutes)
                : gameStats.Values.OrderByDescending(g => g.Players).ThenByDescending(g => g.TotalMinutes))
            .Take(count)
            .ToList();

        var sb = new StringBuilder();
        for (var i = 0; i < top.Count; i++)
        {
            var g = top[i];
            var playerLabel = g.Players == 1 ? "1 player" : $"{g.Players} players";
            sb.AppendLine($"`{i + 1,2}.` **{g.Name}** — {playerLabel} · {EmbedHelpers.FormatHours(g.TotalMinutes / 60.0)} combined");
        }

        var sortLabel = sort == "hours" ? "Total Hours" : "Player Count";
        var embed = new EmbedBuilder()
            .WithColor(SteamColor)
            .WithTitle($"🏆 {Context.Guild.Name} — Top Games by {sortLabel}")
            .WithDescription(sb.ToString())
            .WithFooter($"{guildRegistered.Count} registered member(s) · {gameStats.Count:N0} unique games")
            .WithCurrentTimestamp()
            .Build();

        await FollowupAsync(embed: embed);
    }

    // -----------------------------------------------------------------------
    // /server recentgames [count]
    // -----------------------------------------------------------------------

    [SlashCommand("recentgames", "Games server members have been playing in the last 2 weeks")]
    public async Task ServerRecentGamesAsync(
        [Summary("count", "How many games to show (1–20, default 10)")] int count = 10)
    {
        await DeferAsync();
        count = Math.Clamp(count, 1, 20);

        var guildRegistered = await GetGuildRegisteredUsersAsync();
        if (guildRegistered.Count == 0)
        {
            await FollowupAsync(embed: NoMembersEmbed());
            return;
        }

        // Fetch recently played games for everyone in parallel
        var recentTasks = guildRegistered.Select(r => _steam.GetRecentlyPlayedGamesAsync(r.SteamId));
        var recentResults = await Task.WhenAll(recentTasks);

        // Aggregate by AppId (recent playtime only)
        var gameStats = new Dictionary<int, (string Name, int Players, long RecentMinutes)>();
        foreach (var data in recentResults)
        {
            if (data?.Games is null) continue;
            foreach (var game in data.Games)
            {
                if (gameStats.TryGetValue(game.AppId, out var existing))
                    gameStats[game.AppId] = (existing.Name, existing.Players + 1, existing.RecentMinutes + game.PlaytimeRecentMinutes);
                else
                    gameStats[game.AppId] = (game.Name, 1, game.PlaytimeRecentMinutes);
            }
        }

        if (gameStats.Count == 0)
        {
            await FollowupAsync(embed: new EmbedBuilder()
                .WithColor(SteamColor)
                .WithDescription("No one on the server has played any games in the last 2 weeks.")
                .Build());
            return;
        }

        var top = gameStats.Values
            .OrderByDescending(g => g.Players)
            .ThenByDescending(g => g.RecentMinutes)
            .Take(count)
            .ToList();

        var activeCount = recentResults.Count(r => r?.Games?.Count > 0);

        var sb = new StringBuilder();
        for (var i = 0; i < top.Count; i++)
        {
            var g = top[i];
            var playerLabel = g.Players == 1 ? "1 player" : $"{g.Players} players";
            sb.AppendLine($"`{i + 1,2}.` **{g.Name}** — {playerLabel} · {EmbedHelpers.FormatHours(g.RecentMinutes / 60.0)} past 2 weeks");
        }

        var embed = new EmbedBuilder()
            .WithColor(SteamColor)
            .WithTitle($"🕹️ {Context.Guild.Name} — Server Activity (Last 2 Weeks)")
            .WithDescription(sb.ToString())
            .WithFooter($"{activeCount} active member(s) · {guildRegistered.Count} registered total")
            .WithCurrentTimestamp()
            .Build();

        await FollowupAsync(embed: embed);
    }

    // -----------------------------------------------------------------------
    // Helpers
    // -----------------------------------------------------------------------

    // Returns registered users who are members of the current guild.
    // Uses REST to check membership so no privileged gateway intent is required.
    private async Task<List<Data.Entities.RegisteredUser>> GetGuildRegisteredUsersAsync()
    {
        var allRegistered = await _users.GetAllAsync();
        var memberTasks = allRegistered.Select(async r =>
        {
            var member = await ((IGuild)Context.Guild).GetUserAsync(r.DiscordUserId);
            return (r, isMember: member is not null);
        });
        var results = await Task.WhenAll(memberTasks);
        return results.Where(x => x.isMember).Select(x => x.r).ToList();
    }

    private static Embed NoMembersEmbed() =>
        new EmbedBuilder()
            .WithColor(Color.Red)
            .WithDescription("❌ No server members have linked their Steam accounts yet. Use `/steam register` to get started!")
            .Build();
}
