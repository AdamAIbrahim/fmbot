namespace SteamBot.Discord.Modules;

internal static class EmbedHelpers
{
    internal const uint SteamColor = 0x1b2838;

    internal static string FormatHours(double hours) =>
        hours switch
        {
            < 1 => $"{(int)(hours * 60)}m",
            >= 1000 => $"{hours:N0}h",
            _ => $"{hours:N1}h"
        };
}
