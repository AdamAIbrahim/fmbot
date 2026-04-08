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

    [SlashCommand("addfriend", "Add a server member to your Steam friends list")]
    public async Task AddFriendAsync(
        [Summary("user", "The Discord user to add as a friend")] IUser user)
    {
        await DeferAsync(ephemeral: true);

        if (user.Id == Context.User.Id)
        {
            await FollowupAsync("❌ You can't add yourself as a friend.", ephemeral: true);
            return;
        }

        var selfRegistered = await _users.GetByDiscordIdAsync(Context.User.Id);
        if (selfRegistered is null)
        {
            await FollowupAsync("❌ You haven't linked a Steam account yet. Use `/steam register` first.", ephemeral: true);
            return;
        }

        var friendRegistered = await _users.GetByDiscordIdAsync(user.Id);
        if (friendRegistered is null)
        {
            await FollowupAsync($"❌ **{user.Username}** hasn't linked their Steam account yet.", ephemeral: true);
            return;
        }

        var added = await _users.AddFriendAsync(Context.User.Id, user.Id);
        if (!added)
        {
            await FollowupAsync($"⚠️ **{user.Username}** is already in your friends list.", ephemeral: true);
            return;
        }

        var profile = await _steam.GetPlayerSummaryAsync(friendRegistered.SteamId);
        var embed = new EmbedBuilder()
            .WithColor(0x1b2838)
            .WithTitle("✅ Friend Added")
            .WithDescription($"**[{profile?.PersonaName ?? user.Username}]({profile?.ProfileUrl ?? "#"})** has been added to your Steam friends list.")
            .WithThumbnailUrl(profile?.AvatarUrl)
            .WithFooter("Use /friends to see what your friends are playing")
            .Build();

        await FollowupAsync(embed: embed, ephemeral: true);
    }

    [SlashCommand("removefriend", "Remove a server member from your Steam friends list")]
    public async Task RemoveFriendAsync(
        [Summary("user", "The Discord user to remove")] IUser user)
    {
        await DeferAsync(ephemeral: true);
        var removed = await _users.RemoveFriendAsync(Context.User.Id, user.Id);
        var message = removed
            ? $"✅ **{user.Username}** has been removed from your friends list."
            : $"⚠️ **{user.Username}** isn't in your friends list.";
        await FollowupAsync(message, ephemeral: true);
    }
}
