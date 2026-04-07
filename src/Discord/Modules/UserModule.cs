using Discord;
using Discord.Interactions;
using SteamBot.Data;
using SteamBot.Steam;

namespace SteamBot.Discord.Modules;

// /register <steam_id_or_vanity>
// /unregister
[Group("steam", "Steam account management")]
public class UserModule : InteractionModuleBase<SocketInteractionContext>
{
    private readonly UserRepository _users;
    private readonly SteamApiClient _steam;

    public UserModule(UserRepository users, SteamApiClient steam)
    {
        _users = users;
        _steam = steam;
    }

    [SlashCommand("register", "Link your Steam account to your Discord user")]
    public async Task RegisterAsync(
        [Summary("steam_id", "Your SteamID64, vanity URL name, or full Steam profile URL")] string steamInput)
    {
        await DeferAsync(ephemeral: true);

        var steamId = await _steam.ResolveSteamIdAsync(steamInput);
        if (steamId is null)
        {
            await FollowupAsync("❌ Couldn't resolve that Steam ID or vanity URL. Make sure your profile is public and the name is correct.", ephemeral: true);
            return;
        }

        var profile = await _steam.GetPlayerSummaryAsync(steamId);
        if (profile is null)
        {
            await FollowupAsync("❌ Found the Steam ID but couldn't load the profile. Try again in a moment.", ephemeral: true);
            return;
        }

        await _users.UpsertAsync(Context.User.Id, steamId);

        var embed = new EmbedBuilder()
            .WithColor(0x1b2838) // Steam dark blue
            .WithTitle("✅ Steam Account Linked")
            .WithDescription($"Your Discord account is now linked to **[{profile.PersonaName}]({profile.ProfileUrl})**.")
            .WithThumbnailUrl(profile.AvatarUrl)
            .AddField("Steam ID", $"`{steamId}`", inline: true)
            .WithFooter("Use /steam profile to view your stats")
            .Build();

        await FollowupAsync(embed: embed, ephemeral: true);
    }

    [SlashCommand("unregister", "Unlink your Steam account from your Discord user")]
    public async Task UnregisterAsync()
    {
        await DeferAsync(ephemeral: true);
        var removed = await _users.DeleteAsync(Context.User.Id);
        var message = removed
            ? "✅ Your Steam account has been unlinked."
            : "⚠️ You don't have a Steam account linked.";
        await FollowupAsync(message, ephemeral: true);
    }
}
