using Jellyfin.Plugin.ManualSubtitleExtract.Models;
using Jellyfin.Plugin.ManualSubtitleExtract.Services;
using MediaBrowser.Common.Api;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.IO;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.ManualSubtitleExtract.Controllers;

[ApiController]
[Route("ManualSubtitleExtract")]
[Authorize(Policy = Policies.RequiresElevation)]
public sealed class ManualSubtitleExtractController : ControllerBase
{
    private readonly ILibraryManager _libraryManager;
    private readonly SubtitleProbeService _probe;
    private readonly SubtitleExtractService _extract;
    private readonly IProviderManager _providerManager;
    private readonly IFileSystem _fileSystem;
    private readonly ILogger<ManualSubtitleExtractController> _logger;

    public ManualSubtitleExtractController(
        ILibraryManager libraryManager,
        SubtitleProbeService probe,
        SubtitleExtractService extract,
        IProviderManager providerManager,
        IFileSystem fileSystem,
        ILogger<ManualSubtitleExtractController> logger)
    {
        _libraryManager = libraryManager;
        _probe = probe;
        _extract = extract;
        _providerManager = providerManager;
        _fileSystem = fileSystem;
        _logger = logger;
    }

    /// <summary>
    /// Returns all embedded subtitle tracks for a Jellyfin movie or episode.
    /// </summary>
    [HttpGet("{itemId:guid}/tracks")]
    [ProducesResponseType(
        typeof(IReadOnlyList<SubtitleTrackDto>),
        StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<IReadOnlyList<SubtitleTrackDto>>> GetTracks(
        [FromRoute] Guid itemId,
        CancellationToken cancellationToken)
    {
        if (itemId == Guid.Empty)
        {
            return BadRequest(new
            {
                error = "A valid Jellyfin item ID is required."
            });
        }

        try
        {
            var item = GetLocalItem(itemId);

            var tracks = await _probe
                .GetTracksAsync(
                    item.Path,
                    cancellationToken)
                .ConfigureAwait(false);

            return Ok(tracks);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new
            {
                error = ex.Message
            });
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(
                ex,
                "Could not inspect subtitle tracks for Jellyfin item {ItemId}",
                itemId);

            return BadRequest(new
            {
                error = ex.Message
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Unexpected error while inspecting subtitles for Jellyfin item {ItemId}",
                itemId);

            return StatusCode(
                StatusCodes.Status500InternalServerError,
                new
                {
                    error = "Unexpected error while reading embedded subtitles."
                });
        }
    }

    /// <summary>
    /// Extracts one embedded text subtitle stream to an external SRT file.
    /// </summary>
    [HttpPost("{itemId:guid}/extract")]
    [ProducesResponseType(typeof(ExtractSubtitleResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<ExtractSubtitleResult>> Extract(
        [FromRoute] Guid itemId,
        [FromBody] ExtractSubtitleRequest request,
        CancellationToken cancellationToken)
    {
        if (itemId == Guid.Empty)
        {
            return BadRequest(new
            {
                error = "A valid Jellyfin item ID is required."
            });
        }

        if (request is null)
        {
            return BadRequest(new
            {
                error = "Missing extraction request."
            });
        }

        if (request.StreamIndex < 0)
        {
            return BadRequest(new
            {
                error = "Subtitle stream index cannot be negative."
            });
        }

        try
        {
            var item = GetLocalItem(itemId);

            var result = await _extract
                .ExtractAsync(
                    item.Path,
                    request.StreamIndex,
                    request.Overwrite,
                    cancellationToken)
                .ConfigureAwait(false);

            _logger.LogInformation(
                "Extracted subtitle stream {StreamIndex} from {MediaPath} to {OutputPath}",
                request.StreamIndex,
                item.Path,
                result.OutputPath);

            // Refresh the Jellyfin item so the new external subtitle
            // becomes visible without requiring a full library scan.
            _providerManager.QueueRefresh(
                item.Id,
                new MetadataRefreshOptions(
                    new DirectoryService(_fileSystem)),
                RefreshPriority.High);

            return Ok(result);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new
            {
                error = ex.Message
            });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new
            {
                error = ex.Message
            });
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.LogWarning(
                ex,
                "Jellyfin cannot write subtitle for item {ItemId}",
                itemId);

            return StatusCode(
                StatusCodes.Status403Forbidden,
                new
                {
                    error = "Jellyfin does not have permission to write to the media directory.",
                    details = ex.Message
                });
        }
        catch (IOException ex)
        {
            // Usually means a subtitle with the same filename
            // already exists and overwrite is disabled.
            return Conflict(new
            {
                error = ex.Message
            });
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(
                ex,
                "Subtitle extraction failed for Jellyfin item {ItemId}",
                itemId);

            return BadRequest(new
            {
                error = ex.Message
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Unexpected error while extracting subtitle for Jellyfin item {ItemId}",
                itemId);

            return StatusCode(
                StatusCodes.Status500InternalServerError,
                new
                {
                    error = "Unexpected error while extracting the subtitle."
                });
        }
    }

    /// <summary>
    /// Finds the Jellyfin item and validates that it points to a real
    /// local media file accessible by the Jellyfin process/container.
    /// </summary>
    private BaseItem GetLocalItem(Guid itemId)
    {
        var item = _libraryManager.GetItemById(itemId);

        if (item is null)
        {
            throw new KeyNotFoundException(
                $"Jellyfin item '{itemId}' was not found.");
        }

        if (string.IsNullOrWhiteSpace(item.Path))
        {
throw new InvalidOperationException(
                "This Jellyfin item does not have a local media path.");
        }

        if (!System.IO.File.Exists(item.Path))
        {
            throw new InvalidOperationException(
                $"The media file does not exist or Jellyfin cannot access it: {item.Path}");
        }

        return item;
    }
}
