# Manual Subtitle Extract for Jellyfin

A small Jellyfin 10.11+ plugin for one job: **manually choose an embedded text subtitle from a movie/episode and extract it as an external SRT sidecar**.

It is designed for workflows such as Jellyfin + Bazarr + LAPSE, where you may want to extract a correctly timed embedded English subtitle and then use it as a reference for another external subtitle.

## What it does

- Adds **Extract Embedded Subtitle** to the three-dot menu on a movie/episode details page in Jellyfin Web.
- Shows embedded subtitle language, title, codec, subtitle index, Default/Forced/SDH flags.
- Lets you manually pick the track for that specific movie/episode.
- Converts supported text subtitle streams to SRT with FFmpeg.
- Writes the SRT next to the media file.
- Refreshes the Jellyfin item after extraction.
- Does **not** run automatically and has no scheduled tasks.
- Does **not** modify or remux the source video.
- Shows image-based subtitles (PGS/VobSub) as unsupported rather than pretending they can be converted without OCR.

Example:

```text
Dune (2021).mkv
  embedded ENG ASS (selected manually)

=>

Dune (2021).mkv
Dune (2021).eng.srt
```

## Requirements

- Jellyfin Server 10.11.x
- Jellyfin Web served by the Jellyfin server
- FFmpeg + FFprobe (the standard Jellyfin Docker image normally has Jellyfin FFmpeg)
- Jellyfin must have **write permission** to the media directory
- Admin/elevated Jellyfin account to run extraction

The plugin auto-detects these common Linux paths:

```text
/usr/lib/jellyfin-ffmpeg/ffmpeg
/usr/lib/jellyfin-ffmpeg/ffprobe
/usr/bin/ffmpeg
/usr/bin/ffprobe
```

Custom paths can be set on the plugin configuration page.

## Build locally

The project follows Jellyfin's current 10.11 plugin pattern and targets .NET 9.

```bash
dotnet restore Jellyfin.Plugin.ManualSubtitleExtract/Jellyfin.Plugin.ManualSubtitleExtract.csproj
dotnet publish Jellyfin.Plugin.ManualSubtitleExtract/Jellyfin.Plugin.ManualSubtitleExtract.csproj \
  -c Release \
  -o publish
```

For a manual install, create a plugin folder and copy the DLL:

```bash
mkdir -p /var/lib/jellyfin/plugins/ManualSubtitleExtract
cp publish/Jellyfin.Plugin.ManualSubtitleExtract.dll \
  /var/lib/jellyfin/plugins/ManualSubtitleExtract/
```

Restart Jellyfin afterward.

For Docker, copy the DLL into the `plugins/ManualSubtitleExtract` directory inside your persisted Jellyfin config/data volume, then restart the container.

## Publish to your own GitHub repository

1. Create an empty GitHub repository, e.g. `manual-subtitle-extract-jellyfin`.
2. Upload/push this project as the repository root.
3. Make sure GitHub Actions has **Read and write permissions** under repository Settings → Actions → General → Workflow permissions.
4. Push your first release tag:

```bash
git tag v0.1.0
git push origin v0.1.0
```

The included workflow will:

- build the plugin;
- create `manual-subtitle-extract-v0.1.0.zip`;
- publish a GitHub Release;
- calculate the Jellyfin manifest checksum;
- update `manifest.json` on `main` with your GitHub owner/repository URL.

After the action finishes, your Jellyfin repository URL is:

```text
https://raw.githubusercontent.com/oob-1/jellyfin-manual-subtitle-extractor/master/manifest.json
```

Add it in:

**Jellyfin Dashboard → Plugins → Repositories → Add**

Then go to **Catalog**, install **Manual Subtitle Extract**, and restart Jellyfin.

## Usage

1. Open a movie or episode details page.
2. Open the three-dot menu.
3. Choose **Extract Embedded Subtitle**.
4. Select a text-based embedded subtitle.
5. Click **Extract**.
6. Wait for the success message.
7. Refresh/reopen the item if the new sidecar is not immediately visible.

The output uses the embedded stream's language and flags, for example:

```text
Movie.eng.srt
Movie.eng.forced.srt
Movie.eng.sdh.srt
```

## Docker permissions

Your media mount must be writable by Jellyfin. This will not work if the media is mounted read-only:

```yaml
volumes:
  - /srv/media:/media:ro   # extraction cannot write sidecars
```

Use a writable mount if you want this plugin to create subtitle files:

```yaml
volumes:
  - /srv/media:/media
```

## Scope / limitations

- v0.1 is intentionally focused on **text subtitles → SRT**.
- PGS, VobSub, DVB subtitle images need OCR to become text and are not converted by this plugin.
- The injected menu action is designed for Jellyfin Web item **details pages**. Jellyfin Web DOM details are not a stable public plugin API, so future Jellyfin Web releases may require adjusting the injected JavaScript.
- Sidecar overwrite is disabled by default.

## Why the web UI uses middleware injection

Jellyfin's server plugin API supports REST endpoints and dashboard pages, but there is not a stable general-purpose server-plugin API for adding arbitrary actions to every Jellyfin Web item context menu. This project injects a small client script into the served `index.html` response through ASP.NET middleware, without editing the Jellyfin Web files on disk.

## License

MIT.
