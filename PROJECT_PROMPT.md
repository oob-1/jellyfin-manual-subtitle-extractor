# Project Prompt — Manual Subtitle Extract for Jellyfin

Build a production-quality Jellyfin 10.11+ plugin named **Manual Subtitle Extract**.

The plugin must solve one focused problem: from a movie or episode in Jellyfin Web, the user opens the three-dot menu, chooses **Extract Embedded Subtitle**, sees a modal listing the subtitle streams embedded inside that media file, manually selects one text-based stream, and extracts it as an external `.srt` sidecar next to the video. Nothing should run automatically or on a schedule.

## Primary user workflow

1. A movie/episode already exists in Jellyfin.
2. It contains one or more embedded subtitle streams, for example English, French, Arabic, SDH, or forced subtitles.
3. The user opens the item's three-dot menu in Jellyfin Web.
4. The plugin adds an **Extract Embedded Subtitle** action.
5. Clicking the action opens a clean modal listing embedded subtitle tracks with language, title, codec, subtitle index, default/forced/SDH flags, and whether the stream is text-based or image-based.
6. The user chooses one text subtitle and clicks **Extract**.
7. The server uses `ffprobe` to enumerate streams and `ffmpeg` to extract/convert the selected text stream to SRT without touching the video/audio.
8. The subtitle is written beside the video using Jellyfin-compatible sidecar naming, such as `Movie.en.srt`, `Movie.en.forced.srt`, or `Movie.en.sdh.srt`.
9. Jellyfin refreshes that item so the new external subtitle appears immediately.
10. The original MKV/MP4 and embedded subtitle remain unchanged.

## Important constraints

- Manual only: no scheduled task, no library scan, no automatic extraction.
- Support Movies and Episodes/local video files.
- Text subtitle codecs should be convertible to SRT: SubRip/SRT, ASS/SSA, WebVTT, MOV_TEXT and other FFmpeg text subtitle formats.
- Image subtitles such as PGS/VobSub must be shown but disabled with a clear message that OCR is not provided.
- Never modify/remux the source media file.
- Default behavior must never overwrite an existing sidecar.
- Optional admin setting may allow overwrite, but the UI still must require explicit confirmation.
- Use safe process execution (`ProcessStartInfo.ArgumentList`), never shell string concatenation.
- Create a temporary output and atomically move it into place after FFmpeg succeeds.
- Require elevated/admin permission for filesystem-writing API calls.
- Allow optional custom paths for FFmpeg and FFprobe, while auto-detecting Jellyfin's common bundled paths such as `/usr/lib/jellyfin-ffmpeg/ffmpeg` and `/usr/lib/jellyfin-ffmpeg/ffprobe`.
- The plugin should work in Docker as long as Jellyfin has write access to the media mount.

## Jellyfin integration

Use C#/.NET 9 and Jellyfin 10.11 plugin packages. Implement server REST endpoints for listing embedded tracks and extracting one. Inject a small JavaScript client into the Jellyfin Web `index.html` response via ASP.NET middleware/IStartupFilter rather than editing the web files on disk. This client adds the context-menu action and owns the modal UI.

The plugin repository must include a GitHub Actions release workflow. A tag such as `v0.1.0` should build the DLL, package it into a ZIP GitHub Release, calculate the Jellyfin manifest checksum, and update `manifest.json` on `main`. Users should then be able to add the raw `manifest.json` URL under Jellyfin Dashboard → Plugins → Repositories and install the plugin from Catalog.

Keep the scope deliberately small. Do not implement subtitle synchronization, translation, OCR, Bazarr integration, or background extraction. The plugin exists to turn a manually selected embedded **text** subtitle into an external SRT sidecar with the friendliest possible Jellyfin UI.
