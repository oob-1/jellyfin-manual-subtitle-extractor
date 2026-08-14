using Jellyfin.Plugin.ManualSubtitleExtract.Models;
using Jellyfin.Plugin.ManualSubtitleExtract.Services;
using MediaBrowser.Common.Api;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Model.IO;
using MediaBrowser.Model.Entities;
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

    [HttpGet("{itemId:guid}/tracks")]
    [ProducesResponseType(typeof(IReadOnlyList<SubtitleTrackDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<SubtitleTrackDto>>> GetTracks(
        [FromRoute] Guid itemId,
        CancellationToken cancellationToken)
    {
        var item = GetLocalItem(itemId);
        var tracks = await _probe.GetTracksAsync(item.Path, cancellationToken).ConfigureAwait(false);
        return Ok(tracks);
    }

    [HttpPost("{itemId:guid}/extract")]
    [ProducesResponseType(typeof(ExtractSubtitleResult), StatusCodes.Status200OK)]
    public async Task<ActionResult<ExtractSubtitleResult>> Extract(
        [FromRoute] Guid itemId,
        [FromBody] ExtractSubtitleRequest request,
        CancellationToken cancellationToken)
    {
        var item = GetLocalItem(itemId);
        var result = await _extract.ExtractAsync(item.Path, request.StreamIndex, request.Overwrite, cancellationToken).ConfigureAwait(false);

        _logger.LogInformation("Extracted subtitle stream {StreamIndex} from {Path} to {Output}", request.StreamIndex, item.Path, result.OutputPath);

        _providerManager.QueueRefresh(
            item.Id,
            new MetadataRefreshOptions(new DirectoryService(_fileSystem)),
            RefreshPriority.High);

        return Ok(result);
    }

    private BaseItem GetLocalItem(Guid itemId)
    {
        var item = _libraryManager.GetItemById<BaseItem>(itemId, User.GetUserId());
        if (item is null)
        {
            throw new KeyNotFoundException("Jellyfin item was not found.");
        }

        if (string.IsNullOrWhiteSpace(item.Path) || !System.IO.File.Exists(item.Path))
        {
            throw new InvalidOperationException("This item does not point to a local media file that the Jellyfin server can access.");
        }

        return item;
    }
}
