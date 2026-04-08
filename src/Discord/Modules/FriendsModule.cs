using System.Text;
using Discord;
using Discord.Interactions;
using SteamBot.Data;
using SteamBot.Steam;

namespace SteamBot.Discord.Modules;

// /friends
public class FriendsModule : InteractionModuleBase<SocketInteractionContext>
{
    private readonly UserRepository _users;
    private readonly SteamApiClient _steam;

    private const uint SteamColor = EmbedHelpers.SteamColor;

    public FriendsModule(UserRepository users, SteamApiClient steam)
    {
        _users = users;
        _steam = steam;
    }

    [SlashCommand("friends", "See what your Steam friends are up to right now")]
    public async Task FriendsAsync()
    {
        await DeferAsync();

        if (await _users.GetByDiscordIdAsync(Context.User.Id) is null)
        {
            await FollowupAsync(embed: ErrorEmbed(
                "You haven't linked a Steam account yet. Use `/steam register` to link one."));
            return;
        }

        var friendEntries = await _users.GetFriendsAsync(Context.User.Id);
        if (friendEntries.Count == 0)
        {
            await FollowupAsync(embed: new EmbedBuilder()
                .WithColor(SteamColor)
                .WithTitle("👥 Your Steam Friends")
                .WithDescription(
                    "You haven't added any friends yet.\n" +
                    "Use `/steam addfriend @user` to add someone!")
                .Build());
            return;
        }

        // Resolve each friend's Steam data in parallel
        var tasks = friendEntries.Select(async f =>
        {
            var registered = await _users.GetByDiscordIdAsync(f.FriendDiscordUserId);
            if (registered is null)
                return (DiscordId: f.FriendDiscordUserId, SteamId: (string?)null,
                        Profile: (Steam.Models.PlayerSummary?)null,
                        Recent: (Steam.Models.RecentlyPlayedData?)null);

            var profileTask = _steam.GetPlayerSummaryAsync(registered.SteamId);
            var recentTask = _steam.GetRecentlyPlayedGamesAsync(registered.SteamId);
            await Task.WhenAll(profileTask, recentTask);

            return (DiscordId: f.FriendDiscordUserId, SteamId: (string?)registered.SteamId,
                    Profile: profileTask.Result, Recent: recentTask.Result);
        });

        var results = await Task.WhenAll(tasks);

        // Sort: currently playing first, then by online status, then offline
        var sorted = results.OrderBy(r =>
        {
            if (r.Profile is null) return 3;
            if (!string.IsNullOrWhiteSpace(r.Profile.CurrentGame)) return 0;
            return r.Profile.PersonaState == 0 ? 2 : 1;
        });

        var sb = new StringBuilder();
        foreach (var r in sorted)
        {
            if (r.SteamId is null)
            {
                // Friend hasn't linked Steam — show their Discord name
                var discordUser = await Context.Client.GetUserAsync(r.DiscordId);
                var name = discordUser?.Username ?? r.DiscordId.ToString();
                sb.AppendLine($"🔗 **{name}** — hasn't linked their Steam account");
                continue;
            }

            var profile = r.Profile;
            if (profile is null)
            {
                sb.AppendLine($"❓ *(couldn't load profile)*");
                continue;
            }

            if (!profile.IsProfilePublic)
            {
                sb.AppendLine($"🔒 **{profile.PersonaName}** — profile is private");
                continue;
            }

            var displayName = $"[{profile.PersonaName}]({profile.ProfileUrl})";

            if (!string.IsNullOrWhiteSpace(profile.CurrentGame))
            {
                sb.AppendLine($"🎮 **{displayName}** — ▶ *{profile.CurrentGame}*");
            }
            else
            {
                var statusEmoji = profile.PersonaState switch
                {
                    0 => "⚫",
                    1 => "🟢",
                    _ => "🟡"   // Busy / Away / Snooze
                };

                var lastPlayed = r.Recent?.Games?.FirstOrDefault();
                var suffix = lastPlayed is not null
                    ? $" · Last played: *{lastPlayed.Name}*" +
                      (lastPlayed.PlaytimeRecentMinutes > 0
                          ? $" ({EmbedHelpers.FormatHours(lastPlayed.PlaytimeRecentHours)} past 2 weeks)"
                          : string.Empty)
                    : string.Empty;

                sb.AppendLine($"{statusEmoji} **{displayName}** — {profile.PersonaStateLabel}{suffix}");
            }
        }

        var embed = new EmbedBuilder()
            .WithColor(SteamColor)
            .WithTitle($"👥 {Context.User.Username}'s Steam Friends")
            .WithDescription(sb.ToString())
            .WithFooter($"{friendEntries.Count} friend(s) · Use /steam addfriend to add more")
            .WithCurrentTimestamp()
            .Build();

        await FollowupAsync(embed: embed);
    }

    private static Embed ErrorEmbed(string message) =>
        new EmbedBuilder()
            .WithColor(Color.Red)
            .WithDescription($"❌ {message}")
            .Build();
}
