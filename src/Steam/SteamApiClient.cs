using System.Text.Json;
using Serilog;
using SteamBot.Steam.Models;

namespace SteamBot.Steam;

public class SteamApiClient
{
    private readonly HttpClient _http;
    private readonly string _apiKey;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public SteamApiClient(HttpClient http, string apiKey)
    {
        _http = http;
        _apiKey = apiKey;
    }

    // Resolve a vanity URL (e.g. "gaben") to a SteamID64.
    // Returns null if not found.
    public async Task<string?> ResolveVanityUrlAsync(string vanity)
    {
        try
        {
            var url = $"https://api.steampowered.com/ISteamUser/ResolveVanityURL/v1/?key={_apiKey}&vanityurl={Uri.EscapeDataString(vanity)}";
            var json = await _http.GetStringAsync(url);
            var result = JsonSerializer.Deserialize<VanityUrlResponse>(json, JsonOptions);
            return result?.Response?.Success == 1 ? result.Response.SteamId : null;
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Failed to resolve vanity URL: {Vanity}", vanity);
            return null;
        }
    }

    // Get profile summary for one or more SteamID64s.
    public async Task<PlayerSummary?> GetPlayerSummaryAsync(string steamId)
    {
        try
        {
            var url = $"https://api.steampowered.com/ISteamUser/GetPlayerSummaries/v2/?key={_apiKey}&steamids={steamId}";
            var json = await _http.GetStringAsync(url);
            var result = JsonSerializer.Deserialize<PlayerSummaryResponse>(json, JsonOptions);
            return result?.Response?.Players?.FirstOrDefault();
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Failed to get player summary for {SteamId}", steamId);
            return null;
        }
    }

    // Get all owned games with playtime info.
    public async Task<OwnedGamesData?> GetOwnedGamesAsync(string steamId)
    {
        try
        {
            var url = $"https://api.steampowered.com/IPlayerService/GetOwnedGames/v1/?key={_apiKey}&steamid={steamId}&include_appinfo=true&include_played_free_games=true";
            var json = await _http.GetStringAsync(url);
            var result = JsonSerializer.Deserialize<OwnedGamesResponse>(json, JsonOptions);
            return result?.Response;
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Failed to get owned games for {SteamId}", steamId);
            return null;
        }
    }

    // Get recently played games (last 2 weeks).
    public async Task<RecentlyPlayedData?> GetRecentlyPlayedGamesAsync(string steamId)
    {
        try
        {
            var url = $"https://api.steampowered.com/IPlayerService/GetRecentlyPlayedGames/v1/?key={_apiKey}&steamid={steamId}";
            var json = await _http.GetStringAsync(url);
            var result = JsonSerializer.Deserialize<RecentlyPlayedResponse>(json, JsonOptions);
            return result?.Response;
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Failed to get recently played games for {SteamId}", steamId);
            return null;
        }
    }

    // Get Steam level.
    public async Task<int?> GetPlayerLevelAsync(string steamId)
    {
        try
        {
            var url = $"https://api.steampowered.com/IPlayerService/GetSteamLevel/v1/?key={_apiKey}&steamid={steamId}";
            var json = await _http.GetStringAsync(url);
            var result = JsonSerializer.Deserialize<PlayerLevelResponse>(json, JsonOptions);
            return result?.Response?.PlayerLevel;
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Failed to get Steam level for {SteamId}", steamId);
            return null;
        }
    }

    // Determine if the input is a SteamID64 (17-digit number) or a vanity URL.
    // Returns the resolved SteamID64, or null if it couldn't be resolved.
    public async Task<string?> ResolveSteamIdAsync(string input)
    {
        input = input.Trim();

        // Handle full Steam profile URLs
        if (input.StartsWith("https://steamcommunity.com/", StringComparison.OrdinalIgnoreCase) ||
            input.StartsWith("http://steamcommunity.com/", StringComparison.OrdinalIgnoreCase))
        {
            // https://steamcommunity.com/profiles/76561198XXXXXXXXX
            if (input.Contains("/profiles/"))
            {
                var segment = input.Split("/profiles/", StringSplitOptions.RemoveEmptyEntries).LastOrDefault()?.TrimEnd('/');
                if (segment != null && segment.Length == 17 && long.TryParse(segment, out _))
                    return segment;
            }
            // https://steamcommunity.com/id/vanityname
            if (input.Contains("/id/"))
            {
                var segment = input.Split("/id/", StringSplitOptions.RemoveEmptyEntries).LastOrDefault()?.TrimEnd('/');
                if (!string.IsNullOrEmpty(segment))
                    return await ResolveVanityUrlAsync(segment);
            }
            return null;
        }

        // Raw SteamID64
        if (input.Length == 17 && long.TryParse(input, out _))
            return input;

        // Treat as vanity URL
        return await ResolveVanityUrlAsync(input);
    }
}
