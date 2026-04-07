# SteamBot

A Discord bot that shows Steam game stats for any registered user.

## Features

| Command | Description |
|---|---|
| `/steam register <steam_id>` | Link your Steam account to your Discord user |
| `/steam unregister` | Unlink your Steam account |
| `/profile [user]` | View a Steam profile — level, games owned, total hours, current game |
| `/recentgames [user]` | Games played in the last 2 weeks |
| `/topgames [user] [count]` | Most-played games by total hours (up to 25) |

`steam_id` accepts:
- A **SteamID64** (17-digit number, e.g. `76561197960287930`)
- A **vanity URL name** (e.g. `gaben`)
- A **full Steam profile URL** (`https://steamcommunity.com/id/gaben` or `.../profiles/...`)

## Setup

### Prerequisites
- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- A [Discord bot token](https://discord.com/developers/applications)
- A [Steam Web API key](https://steamcommunity.com/dev/apikey)

### Configuration

Edit `src/appsettings.json`:

```json
{
  "Bot": {
    "DiscordToken": "YOUR_DISCORD_BOT_TOKEN",
    "SteamApiKey": "YOUR_STEAM_API_KEY",
    "DatabasePath": "steambot.db",
    "TestGuildId": null
  }
}
```

- **DiscordToken** — Bot token from the [Discord Developer Portal](https://discord.com/developers/applications)
- **SteamApiKey** — API key from [Steam Dev](https://steamcommunity.com/dev/apikey)
- **DatabasePath** — SQLite file path (created automatically on first run)
- **TestGuildId** — (Optional) Your server's guild ID for instant slash command registration during development. Set to `null` for global registration (takes up to 1 hour to propagate).

You can also override any setting via environment variables prefixed with `STEAMBOT_`, e.g.:
```
STEAMBOT_Bot__DiscordToken=your_token
STEAMBOT_Bot__SteamApiKey=your_key
```

### Discord Bot Settings

In the Discord Developer Portal, enable:
- **`applications.commands`** scope (required for slash commands)
- **`bot`** scope
- No message content intent required — the bot uses slash commands only.

### Running

```bash
dotnet run --project src/SteamBot.csproj
# or publish:
dotnet publish src/SteamBot.csproj -c Release -o ./publish
./publish/SteamBot
```

## Project Structure

```
SteamBot/
├── src/
│   ├── Config/          BotConfig — loaded from appsettings.json
│   ├── Data/            EF Core DbContext + UserRepository (SQLite)
│   │   └── Entities/    RegisteredUser entity
│   ├── Steam/           Steam Web API client + response models
│   │   └── Models/
│   ├── Discord/         Discord.Net bot client
│   │   └── Modules/
│   │       ├── UserModule.cs    /steam register|unregister
│   │       └── SteamModule.cs   /profile /recentgames /topgames
│   ├── Program.cs       Entry point — DI wiring, DB bootstrap, startup
│   ├── SteamBot.csproj
│   └── appsettings.json
└── SteamBot.slnx
```

**Stack:**
- [Discord.Net](https://github.com/discord-net/Discord.Net) — Discord API
- [EF Core + SQLite](https://learn.microsoft.com/en-us/ef/core/) — user registration persistence
- [Serilog](https://serilog.net/) — structured logging
- Steam Web API via plain `HttpClient` + `System.Text.Json` — no third-party SDK
