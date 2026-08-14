using System.Reflection;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Jellyfin.Plugin.ManualSubtitleExtract.Controllers;

[ApiController]
[Route("ManualSubtitleExtract")]
public sealed class ClientScriptController : ControllerBase
{
    [HttpGet("client.js")]
    [AllowAnonymous]
    [Produces("application/javascript")]
    public IActionResult GetClientScript()
    {
        var assembly = Assembly.GetExecutingAssembly();
        const string resourceName = "Jellyfin.Plugin.ManualSubtitleExtract.Configuration.manual-subtitle-extract.js";
        using var stream = assembly.GetManifestResourceStream(resourceName);
        if (stream is null)
        {
            return NotFound();
        }

        using var reader = new StreamReader(stream);
        Response.Headers.CacheControl = "no-cache, no-store, must-revalidate";
        return Content(reader.ReadToEnd(), "application/javascript");
    }
}
