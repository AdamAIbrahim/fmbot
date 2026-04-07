using System.Text.Json.Serialization;

namespace SteamBot.Steam.Models;

// --- Player Summary ---

public class PlayerSummaryResponse
{
    [JsonPropertyName("response")]
    public PlayerSummaryData? Response { get; set; }
}

public class PlayerSummaryData
{
    [JsonPropertyName("players")]
    public List<PlayerSummary>? Players { get; set; }
}

public class PlayerSummary
{
    [JsonPropertyName("steamid")]
    public string SteamId { get; set; } = string.Empty;

    [JsonPropertyName("personaname")]
    public string PersonaName { get; set; } = string.Empty;

    [JsonPropertyName("profileurl")]
    public string ProfileUrl { get; set; } = string.Empty;

    [JsonPropertyName("avatarfull")]
    public string AvatarUrl { get; set; } = string.Empty;

    [JsonPropertyName("personastate")]
    public int PersonaState { get; set; }

    [JsonPropertyName("communityvisibilitystate")]
    public int CommunityVisibilityState { get; set; }

    [JsonPropertyName("timecreated")]
    public long? TimeCreated { get; set; }

    [JsonPropertyName("loccountrycode")]
    public string? CountryCode { get; set; }

    [JsonPropertyName("gameextrainfo")]
    public string? CurrentGame { get; set; }

    public string PersonaStateLabel => PersonaState switch
    {
        0 => "Offline",
        1 => "Online",
        2 => "Busy",
        3 => "Away",
        4 => "Snooze",
        5 => "Looking to trade",
        6 => "Looking to play",
        _ => "Unknown"
    };

    public bool IsProfilePublic => CommunityVisibilityState == 3;
}

// --- Owned Games ---

public class OwnedGamesResponse
{
    [JsonPropertyName("response")]
    public OwnedGamesData? Response { get; set; }
}

public class OwnedGamesData
{
    [JsonPropertyName("game_count")]
    public int GameCount { get; set; }

    [JsonPropertyName("games")]
    public List<OwnedGame>? Games { get; set; }
}

public class OwnedGame
{
    [JsonPropertyName("appid")]
    public int AppId { get; set; }

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("playtime_forever")]
    public int PlaytimeMinutes { get; set; }

    [JsonPropertyName("playtime_2weeks")]
    public int? PlaytimeRecentMinutes { get; set; }

    [JsonPropertyName("img_icon_url")]
    public string IconHash { get; set; } = string.Empty;

    public double PlaytimeHours => Math.Round(PlaytimeMinutes / 60.0, 1);
    public double PlaytimeRecentHours => Math.Round((PlaytimeRecentMinutes ?? 0) / 60.0, 1);
    public string HeaderImageUrl => $"https://cdn.akamai.steamstatic.com/steam/apps/{AppId}/header.jpg";
}

// --- Recently Played Games ---

public class RecentlyPlayedResponse
{
    [JsonPropertyName("response")]
    public RecentlyPlayedData? Response { get; set; }
}

public class RecentlyPlayedData
{
    [JsonPropertyName("total_count")]
    public int TotalCount { get; set; }

    [JsonPropertyName("games")]
    public List<RecentGame>? Games { get; set; }
}

public class RecentGame
{
    [JsonPropertyName("appid")]
    public int AppId { get; set; }

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("playtime_2weeks")]
    public int PlaytimeRecentMinutes { get; set; }

    [JsonPropertyName("playtime_forever")]
    public int PlaytimeForeverMinutes { get; set; }

    [JsonPropertyName("img_icon_url")]
    public string IconHash { get; set; } = string.Empty;

    public double PlaytimeRecentHours => Math.Round(PlaytimeRecentMinutes / 60.0, 1);
    public double PlaytimeForeverHours => Math.Round(PlaytimeForeverMinutes / 60.0, 1);
    public string HeaderImageUrl => $"https://cdn.akamai.steamstatic.com/steam/apps/{AppId}/header.jpg";
    public string StoreUrl => $"https://store.steampowered.com/app/{AppId}";
}

// --- Vanity URL Resolve ---

public class VanityUrlResponse
{
    [JsonPropertyName("response")]
    public VanityUrlData? Response { get; set; }
}

public class VanityUrlData
{
    [JsonPropertyName("steamid")]
    public string? SteamId { get; set; }

    [JsonPropertyName("success")]
    public int Success { get; set; }

    [JsonPropertyName("message")]
    public string? Message { get; set; }
}

// --- Player Level ---

public class PlayerLevelResponse
{
    [JsonPropertyName("response")]
    public PlayerLevelData? Response { get; set; }
}

public class PlayerLevelData
{
    [JsonPropertyName("player_level")]
    public int PlayerLevel { get; set; }
}
