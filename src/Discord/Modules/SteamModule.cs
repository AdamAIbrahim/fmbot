using Discord;
using Discord.Interactions;
using SteamBot.Data;
using SteamBot.Steam;
using SteamBot.Steam.Models;

namespace SteamBot.Discord.Modules;

// /profile [user]
// /recentgames [user]
// /topgames [user]
public class SteamModule : InteractionModuleBase<SocketInteractionContext>
{
    private readonly UserRepository _users;
    private readonly SteamApiClient _steam;

    // Steam dark blue color used across embeds
    private const uint SteamColor = 0x1b2838;

    public SteamModule(UserRepository users, SteamApiClient steam)
    {
        _users = users;
        _steam = steam;
    }

    // -----------------------------------------------------------------------
    // /profile [user]
    // -----------------------------------------------------------------------

    [SlashCommand("profile", "View a Steam profile's stats")]
    public async Task ProfileAsync(
        [Summary("user", "Discord user (defaults to you)")] IUser? discordUser = null)
    {
        await DeferAsync();

        var targetUser = discordUser ?? Context.User;
        var (steamId, errorEmbed) = await ResolveRegisteredUserAsync(targetUser);
        if (steamId is null) { await FollowupAsync(embed: errorEmbed); return; }

        var (profile, level, owned) = await (
            _steam.GetPlayerSummaryAsync(steamId),
            _steam.GetPlayerLevelAsync(steamId),
            _steam.GetOwnedGamesAsync(steamId)
        ).WhenAll();

        if (profile is null)
        {
            await FollowupAsync(embed: ErrorEmbed("Couldn't load Steam profile. Try again later."));
            return;
        }

        if (!profile.IsProfilePublic)
        {
            await FollowupAsync(embed: ErrorEmbed($"**{profile.PersonaName}**'s profile is private."));
            return;
        }

        var totalHours = owned?.Games?.Sum(g => g.PlaytimeMinutes / 60.0) ?? 0;
        var gamesPlayed = owned?.Games?.Count(g => g.PlaytimeMinutes > 0) ?? 0;

        var embed = new EmbedBuilder()
            .WithColor(SteamColor)
            .WithAuthor(profile.PersonaName, profile.AvatarUrl, profile.ProfileUrl)
            .WithTitle("Steam Profile")
            .WithUrl(profile.ProfileUrl)
            .WithThumbnailUrl(profile.AvatarUrl);

        if (level.HasValue)
            embed.AddField("Level", level.Value, inline: true);

        if (owned != null)
        {
            embed.AddField("Games Owned", $"{owned.GameCount:N0}", inline: true);
            embed.AddField("Games Played", $"{gamesPlayed:N0}", inline: true);
            embed.AddField("Total Playtime", FormatHours(totalHours), inline: true);
        }

        if (!string.IsNullOrWhiteSpace(profile.CurrentGame))
            embed.AddField("▶ Currently Playing", profile.CurrentGame, inline: false);

        if (!string.IsNullOrWhiteSpace(profile.CountryCode))
            embed.AddField("Country", $":flag_{profile.CountryCode.ToLower()}:", inline: true);

        embed.AddField("Status", profile.PersonaStateLabel, inline: true);

        if (profile.TimeCreated.HasValue)
        {
            var created = DateTimeOffset.FromUnixTimeSeconds(profile.TimeCreated.Value);
            embed.AddField("Member Since", $"<t:{profile.TimeCreated.Value}:D>", inline: true);
        }

        embed.WithFooter($"SteamID: {steamId}")
             .WithCurrentTimestamp();

        await FollowupAsync(embed: embed.Build());
    }

    // -----------------------------------------------------------------------
    // /recentgames [user]
    // -----------------------------------------------------------------------

    [SlashCommand("recentgames", "View recently played games (last 2 weeks)")]
    public async Task RecentGamesAsync(
        [Summary("user", "Discord user (defaults to you)")] IUser? discordUser = null)
    {
        await DeferAsync();

        var targetUser = discordUser ?? Context.User;
        var (steamId, errorEmbed) = await ResolveRegisteredUserAsync(targetUser);
        if (steamId is null) { await FollowupAsync(embed: errorEmbed); return; }

        var profile = await _steam.GetPlayerSummaryAsync(steamId);
        if (profile is not null && !profile.IsProfilePublic)
        {
            await FollowupAsync(embed: ErrorEmbed($"**{profile.PersonaName}**'s profile is private."));
            return;
        }

        var recent = await _steam.GetRecentlyPlayedGamesAsync(steamId);

        var displayName = profile?.PersonaName ?? steamId;

        if (recent?.Games is null || recent.Games.Count == 0)
        {
            await FollowupAsync(embed: new EmbedBuilder()
                .WithColor(SteamColor)
                .WithDescription($"**{displayName}** hasn't played any games in the past 2 weeks.")
                .Build());
            return;
        }

        var sb = new System.Text.StringBuilder();
        foreach (var game in recent.Games.Take(10))
        {
            sb.AppendLine($"**[{game.Name}]({game.StoreUrl})**");
            sb.AppendLine($"> {FormatHours(game.PlaytimeRecentHours)} recently · {FormatHours(game.PlaytimeForeverHours)} total");
        }

        if (recent.Games.Count > 10)
            sb.AppendLine($"*…and {recent.Games.Count - 10} more*");

        var topGame = recent.Games.First();

        var embed = new EmbedBuilder()
            .WithColor(SteamColor)
            .WithAuthor($"{displayName}'s Recent Games", profile?.AvatarUrl, profile?.ProfileUrl)
            .WithDescription(sb.ToString())
            .WithImageUrl(topGame.HeaderImageUrl)
            .WithFooter($"{recent.Games.Count} game(s) played · SteamID: {steamId}")
            .WithCurrentTimestamp()
            .Build();

        await FollowupAsync(embed: embed);
    }

    // -----------------------------------------------------------------------
    // /topgames [user] [count]
    // -----------------------------------------------------------------------

    [SlashCommand("topgames", "View most-played games by total hours")]
    public async Task TopGamesAsync(
        [Summary("user", "Discord user (defaults to you)")] IUser? discordUser = null,
        [Summary("count", "How many games to show (1–25, default 10)")] int count = 10)
    {
        await DeferAsync();

        count = Math.Clamp(count, 1, 25);

        var targetUser = discordUser ?? Context.User;
        var (steamId, errorEmbed) = await ResolveRegisteredUserAsync(targetUser);
        if (steamId is null) { await FollowupAsync(embed: errorEmbed); return; }

        var profile = await _steam.GetPlayerSummaryAsync(steamId);
        if (profile is not null && !profile.IsProfilePublic)
        {
            await FollowupAsync(embed: ErrorEmbed($"**{profile.PersonaName}**'s profile is private."));
            return;
        }

        var owned = await _steam.GetOwnedGamesAsync(steamId);
        var displayName = profile?.PersonaName ?? steamId;

        if (owned?.Games is null || owned.Games.Count == 0)
        {
            await FollowupAsync(embed: new EmbedBuilder()
                .WithColor(SteamColor)
                .WithDescription($"**{displayName}** doesn't have any games or their library is private.")
                .Build());
            return;
        }

        var top = owned.Games
            .Where(g => g.PlaytimeMinutes > 0)
            .OrderByDescending(g => g.PlaytimeMinutes)
            .Take(count)
            .ToList();

        if (top.Count == 0)
        {
            await FollowupAsync(embed: new EmbedBuilder()
                .WithColor(SteamColor)
                .WithDescription($"**{displayName}** hasn't played any games yet.")
                .Build());
            return;
        }

        var sb = new System.Text.StringBuilder();
        for (var i = 0; i < top.Count; i++)
        {
            var game = top[i];
            var storeUrl = $"https://store.steampowered.com/app/{game.AppId}";
            sb.AppendLine($"`{i + 1,2}.` **[{game.Name}]({storeUrl})** — {FormatHours(game.PlaytimeHours)}");
        }

        var topGame = top.First();
        var totalHours = owned.Games.Sum(g => g.PlaytimeMinutes / 60.0);

        var embed = new EmbedBuilder()
            .WithColor(SteamColor)
            .WithAuthor($"{displayName}'s Top {top.Count} Games", profile?.AvatarUrl, profile?.ProfileUrl)
            .WithDescription(sb.ToString())
            .WithImageUrl(topGame.HeaderImageUrl)
            .WithFooter($"{owned.GameCount:N0} games owned · {FormatHours(totalHours)} total · SteamID: {steamId}")
            .WithCurrentTimestamp()
            .Build();

        await FollowupAsync(embed: embed);
    }

    // -----------------------------------------------------------------------
    // Helpers
    // -----------------------------------------------------------------------

    private async Task<(string? SteamId, Embed? Error)> ResolveRegisteredUserAsync(IUser discordUser)
    {
        var registered = await _users.GetByDiscordIdAsync(discordUser.Id);
        if (registered is not null)
            return (registered.SteamId, null);

        var isSelf = discordUser.Id == Context.User.Id;
        var msg = isSelf
            ? "You haven't linked a Steam account yet. Use `/steam register` to link one."
            : $"**{discordUser.Username}** hasn't linked a Steam account.";

        return (null, ErrorEmbed(msg));
    }

    private static Embed ErrorEmbed(string message) =>
        new EmbedBuilder()
            .WithColor(Color.Red)
            .WithDescription($"❌ {message}")
            .Build();

    private static string FormatHours(double hours) =>
        hours switch
        {
            < 1 => $"{(int)(hours * 60)}m",
            >= 1000 => $"{hours:N0}h",
            _ => $"{hours:N1}h"
        };
}

// Tuple deconstruction helper for 3-way Task.WhenAll
file static class TaskExtensions
{
    public static async Task<(T1, T2, T3)> WhenAll<T1, T2, T3>(
        this (Task<T1> t1, Task<T2> t2, Task<T3> t3) tasks)
    {
        await Task.WhenAll(tasks.t1, tasks.t2, tasks.t3);
        return (tasks.t1.Result, tasks.t2.Result, tasks.t3.Result);
    }
}
