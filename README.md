# TelJel

Jellyfin plugin that sends rich Telegram notifications when **movies** and **TV episodes** are added to your libraries.

Inspired by a filesystem watcher workflow: poster + metadata captions, TV episode batching, and routing to Telegram groups linked to specific Jellyfin libraries — without mapping Jellyfin user accounts.

## Features

- Item-added notifications for movies and episodes
- HTML captions with rating, certification, genres, and overview (from Jellyfin metadata)
- Optional poster images via your public Jellyfin URL
- Telegram **groups** with per-group library filters
- Configurable TV batch delay (combine a season dump into one message)
- Test button per group

## Install (from repository)

1. In Jellyfin go to **Dashboard > Plugins > Repositories**
2. Add a repository:

Repository name: `TelJel`

Repository URL:

```text
https://raw.githubusercontent.com/retrorat1/Jellyfin.Plugin.TelJel/main/manifest.json
```

3. Go to **Catalog**, find **TelJel**, install
4. Restart Jellyfin
5. Open **Dashboard > Plugins > TelJel**

## Install (manual)

1. Build the plugin (see below) or download a release zip.
2. Copy `Jellyfin.Plugin.TelJel.dll` (and `meta.json`) into a versioned folder under your Jellyfin plugins directory, e.g.:
   - Windows: `%LOCALAPPDATA%\jellyfin\plugins\TelJel_1.0.0.0\`
   - Windows (ProgramData installs): `%ProgramData%\Jellyfin\Server\plugins\TelJel_1.0.0.0\`
   - Linux: `/var/lib/jellyfin/plugins/TelJel_1.0.0.0/`
   - Docker: `/config/plugins/TelJel_1.0.0.0/` or `/config/data/plugins/TelJel_1.0.0.0/`
3. Restart Jellyfin.
4. Open **Dashboard > Plugins > TelJel**.

## Configure

1. Paste your Telegram **bot token** (from [@BotFather](https://t.me/botfather)).
2. Set your **public Jellyfin URL** so Telegram can fetch posters (e.g. `https://jellyfin.example.com`).
3. Add a **Telegram group**:
   - Name (label only)
   - Chat id (group/channel id, often starts with `-100`)
   - Optional thread id for forum topics
   - Select libraries (none selected = all libraries)
4. Save, then use **Test**.

### Getting a chat id

1. Add the bot to the group (and grant it permission to post).
2. Send a message in the group.
3. Open `https://api.telegram.org/bot<TOKEN>/getUpdates` and read `chat.id`.

## Build / package

Requires [.NET 9 SDK](https://dotnet.microsoft.com/download/dotnet/9.0).

```powershell
.\build-and-package.ps1 -Version "1.0.0.0" -Changelog "Initial release"
```

Then create a GitHub release tagged `v1.0.0.0` and upload `Jellyfin.Plugin.TelJel.zip`. Commit/push the updated `manifest.json`.

Or build only:

```bash
dotnet build Jellyfin.Plugin.TelJel.sln -c Release
```

Output DLL:

`Jellyfin.Plugin.TelJel/bin/Release/net9.0/Jellyfin.Plugin.TelJel.dll`

Built against Jellyfin **10.11** packages. `manifest.json` uses a broad `targetAbi` of `10.0.0.0` for install compatibility.

## How it works

```text
Jellyfin ItemAdded (Movie / Episode)
        │
        ▼
  Match Telegram groups by library
        │
        ├── Movie  → notify immediately (after short metadata wait)
        └── Episode → batch by series+season → one message
        │
        ▼
  Telegram sendMessage / sendPhoto
```

## Not in v1

- Playback / user / plugin system events
- Sports / TheSportsDB enrichment
- Per-Jellyfin-user routing

## License

See [LICENSE](LICENSE) (inherits the Jellyfin plugin template license).
