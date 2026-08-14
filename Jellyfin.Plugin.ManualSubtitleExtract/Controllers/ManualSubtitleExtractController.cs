using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Plugin.ManualSubtitleExtract.Models;
using Jellyfin.Plugin.ManualSubtitleExtract.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Jellyfin.Plugin.ManualSubtitleExtract.Controllers;

[ApiController]
[Authorize]
[Route("ManualSubtitleExtract")]
public class ManualSubtitleExtractController : ControllerBase
{
    private readonly SubtitleProbeService _probeService;
    private readonly SubtitleExtractService _extractService;

    public ManualSubtitleExtractController(
        SubtitleProbeService probeService,
        SubtitleExtractService extractService)
    {
        _probeService = probeService;
        _extractService = extractService;
    }

    /// <summary>
    /// Returns the embedded subtitle streams for a Jellyfin item.
    /// </summary>
    /// <param name="itemId">Jellyfin movie/episode item ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Embedded subtitle tracks.</returns>
    [HttpGet("{itemId:guid}/tracks")]
    [ProducesResponseType(typeof(IReadOnlyList<SubtitleTrackDto>), 200)]
    [ProducesResponseType(400)]
    [ProducesResponseType(401)]
    [ProducesResponseType(404)]
    [ProducesResponseType(500)]
    public async Task<ActionResult<IReadOnlyList<SubtitleTrackDto>>> GetTracks(
        Guid itemId,
        CancellationToken cancellationToken)
    {
        if (itemId == Guid.Empty)
        {
            return BadRequest("A valid Jellyfin item ID is required.");
        }

        try
        {
            var tracks = await _probeService
                .GetTracksAsync(itemId, cancellationToken)
                .ConfigureAwait(false);

            return Ok(tracks);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ex.Message);
        }
        catch (InvalidOperationException ex)
        {
            return StatusCode(500, new
            {
                error = ex.Message
            });
        }
    }

    /// <summary>
    /// Extracts one embedded subtitle stream as an external sidecar subtitle.
    /// </summary>
    /// <param name="itemId">Jellyfin movie/episode item ID.</param>
    /// <param name="request">Extraction request.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Extraction result.</returns>
    [HttpPost("{itemId:guid}/extract")]
    [ProducesResponseType(typeof(ExtractSubtitleResult), 200)]
    [ProducesResponseType(400)]
    [ProducesResponseType(401)]
    [ProducesResponseType(404)]
    [ProducesResponseType(409)]
    [ProducesResponseType(500)]
    public async Task<ActionResult<ExtractSubtitleResult>> Extract(
        Guid itemId,
        [FromBody] ExtractSubtitleRequest request,
        CancellationToken cancellationToken)
    {
        if (itemId == Guid.Empty)
        {
            return BadRequest("A valid Jellyfin item ID is required.");
        }

        if (request is null)
        {
            return BadRequest("Missing extraction request.");
        }

        if (request.StreamIndex < 0)
        {
            return BadRequest("Subtitle stream index cannot be negative.");
        }

        try
        {
            var result = await _extractService
                .ExtractAsync(
                    itemId,
                    request.StreamIndex,
                    cancellationToken)
                .ConfigureAwait(false);

            return Ok(result);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ex.Message);
        }
        catch (UnauthorizedAccessException ex)
        {
            return StatusCode(403, new
            {
                error = ex.Message
            });
        }
        catch (System.IO.
IOException ex)
        {
            return Conflict(new
            {
                error = ex.Message
            });
        }
        catch (InvalidOperationException ex)
        {
            return StatusCode(500, new
            {
                error = ex.Message
            });
        }
    }
}
