# Jellyfin Bulk Download

A Jellyfin plugin that lets you download an entire playlist, TV series, season, or audiobook as a single ZIP file.

## Requirements

- Jellyfin 10.11 or later

## Installation

### Via Plugin Catalog (recommended)

1. In Jellyfin, go to **Dashboard → Plugins → Catalog**.
2. Click the **⋮** menu → **Repositories**.
3. Add a new repository:
   - **Repository name:** FaithBranch Plugins
   - **Repository URL:** `https://faith-branch-software-llc.github.io/Jellyfin-Plugin-BulkDownload/repository.json`
4. Save, then return to the Catalog and search for **Bulk Download**.
5. Install and restart Jellyfin.

### Manual

1. Download the latest `jellyfin-bulk-download_x.x.x.zip` from the [Releases](https://github.com/FaithBranch/jellyfin-bulk-download/releases) page.
2. Extract the ZIP. You should have `Jellyfin.Plugin.BulkDownload.dll` and `meta.json`.
3. Copy both files into your Jellyfin plugins folder:
   - **Linux/Docker:** `/var/lib/jellyfin/plugins/BulkDownload/`
   - **Windows:** `%PROGRAMDATA%\Jellyfin\Server\plugins\BulkDownload\`
4. Restart Jellyfin.

## Usage

1. Open the Jellyfin web interface in a browser.
2. Go to **Dashboard → Plugins → Bulk Download**.
3. Select a tab — **Playlists**, **TV Shows**, or **Audiobooks**.
4. Select the item you want to download. For TV Shows you can expand a series to pick a specific season.
5. Click **Download ZIP**. The browser will download a ZIP containing all the media files.

> **Note:** Downloads work in the Jellyfin web interface (browser). The native iOS and Android apps are not currently supported.

## ZIP structure

| Type | Layout inside ZIP |
|------|-------------------|
| Playlist | flat — all files at root |
| Series | `Series Name/Season X/episode.ext` |
| Season | `Series Name/Season X/episode.ext` |
| Audiobook / Album | `Album Name/track.ext` |

## Building from source

```bash
git clone https://github.com/FaithBranch/jellyfin-bulk-download.git
cd jellyfin-bulk-download
dotnet build --configuration Release
```

The compiled DLL is at `Jellyfin.Plugin.BulkDownload/bin/Release/net9.0/Jellyfin.Plugin.BulkDownload.dll`.

## Releasing a new version

Push a tag in the form `vMAJOR.MINOR.PATCH` (e.g. `v0.1.10`). GitHub Actions will build the plugin, create a release with the ZIP, and update the plugin manifest automatically.

```bash
git tag v0.1.10
git push origin v0.1.10
```
