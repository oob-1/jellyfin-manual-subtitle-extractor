using System.Text.Json.Serialization;

namespace Jellyfin.Plugin.ManualSubtitleExtract.Models;

internal sealed class FfprobeResult
{
    [JsonPropertyName("streams")]
    public List<FfprobeStream> Streams { get; set; } = new();
}

internal sealed class FfprobeStream
{
    [JsonPropertyName("index")]
    public int Index { get; set; }

    [JsonPropertyName("codec_name")]
    public string CodecName { get; set; } = string.Empty;

    [JsonPropertyName("codec_type")]
    public string CodecType { get; set; } = string.Empty;

    [JsonPropertyName("tags")]
    public Dictionary<string, string>? Tags { get; set; }

    [JsonPropertyName("disposition")]
    public FfprobeDisposition? Disposition { get; set; }
}

internal sealed class FfprobeDisposition
{
    [JsonPropertyName("default")]
    public int Default { get; set; }

    [JsonPropertyName("forced")]
    public int Forced { get; set; }

    [JsonPropertyName("hearing_impaired")]
    public int HearingImpaired { get; set; }
}
