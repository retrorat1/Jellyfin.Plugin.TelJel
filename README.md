# TelJel

**TelJel** is a Jellyfin plugin that sends Telegram notifications when new **movies** and **TV episodes** are added to your libraries.

It is aimed at home servers and shared family/friend groups where you want a clear “something new arrived” message — with poster, title, rating, genres, and plot — without wiring every Jellyfin user account to Telegram.

Notifications are routed by **library**. Each Telegram destination (group, channel, or chat) can be linked to the libraries you choose, so different audiences only see what they should (for example kids libraries vs adult libraries).

## What you get

When media is added and Jellyfin finishes indexing it, TelJel can send:

- An HTML-formatted Telegram message with title, library name, rating, certification, genres, and overview
- An optional poster image (using your public Jellyfin URL)
- One combined message when many TV episodes from the same season land together (configurable batch delay)
- A per-group **Test** button so you can verify bot token and chat id quickly

TelJel uses **Jellyfin’s own metadata** (no extra TMDb/API keys required for the message content).

## Features

- **Item added** notifications for movies and episodes
- **Library → Telegram group** routing (tick which libraries each group receives)
- **Select all / Clear** helpers for library selection
- Rich captions from Jellyfin metadata (rating, certification, genres, overview)
- Optional posters via `sendPhoto`
- TV episode batching (one message per series/season burst instead of one per file)
- Optional Telegram **forum topic** thread id (for groups that use Topics)
- Simple settings page inside Jellyfin

## Requirements

- Jellyfin **10.11+** recommended (built against 10.11 packages)
- A Telegram bot token from [@BotFather](https://t.me/botfather)
- The bot added to your group/channel with permission to post
- For posters: a Jellyfin base URL that Telegram’s servers can reach (public HTTPS URL, tunnel, etc.). Local-only URLs still allow text notifications; images may fail and fall back to text

## Install (repository)

1. Open **Dashboard > Plugins > Repositories**
2. Add a repository:

   - **Name:** `TelJel`
   - **URL:**

```text
https://raw.githubusercontent.com/retrorat1/Jellyfin.Plugin.TelJel/main/manifest.json
```

3. Open **Catalog**, find **TelJel**, install
4. Restart Jellyfin when prompted
5. Open **Dashboard > Plugins > TelJel** to configure

## Install (manual)

1. Download the latest release zip from [Releases](https://github.com/retrorat1/Jellyfin.Plugin.TelJel/releases)
2. Extract into a versioned plugin folder, for example:
   - Windows: `%LOCALAPPDATA%\jellyfin\plugins\TelJel_1.0.2.0\`
   - Windows (some installs): `%ProgramData%\Jellyfin\Server\plugins\TelJel_1.0.2.0\`
   - Linux: `/var/lib/jellyfin/plugins/TelJel_1.0.2.0/`
   - Docker: `/config/plugins/TelJel_1.0.2.0/` or `/config/data/plugins/TelJel_1.0.2.0/`
3. Restart Jellyfin
4. Configure under **Dashboard > Plugins > TelJel**

> Tip: Match the plugins folder your other plugins already use (on many Windows desktop installs that is `%LOCALAPPDATA%\jellyfin\plugins`).

## Configuration

### Basic settings

| Setting | Purpose |
|--------|---------|
| **Enable notifications** | Master on/off switch |
| **Telegram bot token** | Token from BotFather |
| **Public Jellyfin URL** | Base URL used to build poster links (`https://jellyfin.example.com`) |
| **TV batch delay** | Seconds to wait after the last new episode before sending one combined TV message |

### Telegram groups

Add one entry per destination chat:

| Field | Purpose |
|------|---------|
| **Name** | Label for you in the settings UI |
| **Chat id** | Group/channel/user chat id (often starts with `-100` for groups) |
| **Thread id** *(optional)* | Forum topic id if you use Telegram Topics; leave blank for normal groups |
| **Libraries** | Tick the Jellyfin libraries this chat should receive (**unticked = not included**) |
| **Silent notification** | Send without sound |
| **Test** | Saves config and sends a test message to that chat |

Typical setup:

1. Create a bot with BotFather and copy the token
2. Add the bot to your Telegram group
3. Add a TelJel group entry with the chat id
4. Tick the libraries for that group (or **Select all**)
5. Save, then press **Test**

### Getting a chat id

1. Add the bot to the group and send a message there
2. Open:

```text
https://api.telegram.org/bot<YOUR_BOT_TOKEN>/getUpdates
```

3. Find `"chat":{"id": ...}` and paste that value into **Chat id**

## How it works

```text
Jellyfin ItemAdded (Movie / Episode)
        │
        ▼
  Match Telegram groups by selected libraries
        │
        ├── Movie   → notify after a short metadata wait
        └── Episode → batch by series + season → one message
        │
        ▼
  Telegram sendMessage / sendPhoto
```

Only **movies** and **episodes** trigger notifications. Series/season container items are ignored so you do not get noise when folders are created.

## Out of scope (for now)

- Playback start/stop notifications
- User login / plugin system events
- Sports-specific TheSportsDB enrichment
- Mapping each Jellyfin user account to a Telegram chat

## Build from source

Requires [.NET 9 SDK](https://dotnet.microsoft.com/download/dotnet/9.0).

```bash
dotnet build Jellyfin.Plugin.TelJel.sln -c Release
```

Output:

`Jellyfin.Plugin.TelJel/bin/Release/net9.0/Jellyfin.Plugin.TelJel.dll`

To publish: create a release zip with the DLL (and `meta.json`), upload it to GitHub Releases, then update `manifest.json` with the `sourceUrl` and MD5 `checksum`.

## License

See [LICENSE](LICENSE) (from the Jellyfin plugin template).
