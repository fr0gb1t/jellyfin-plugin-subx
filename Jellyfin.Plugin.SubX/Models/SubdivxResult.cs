using System.Text.Json.Serialization;

namespace Jellyfin.Plugin.SubX.Models;

public sealed class SubdivxSearchResponse
{
    [JsonPropertyName("aaData")]
    public List<SubdivxItem> Items { get; set; } = new();
}

public sealed class SubdivxItem
{
    [JsonPropertyName("id")]
    public long Id { get; set; }

    [JsonPropertyName("titulo")]
    public string? Title { get; set; }

    [JsonPropertyName("descripcion")]
    public string? Description { get; set; }

    [JsonPropertyName("descargas")]
    public int DownloadCount { get; set; }

    [JsonPropertyName("comentarios")]
    public int CommentCount { get; set; }

    [JsonPropertyName("formato")]
    public string? Format { get; set; }

    [JsonPropertyName("fecha_subida")]
    public string? UploadedAt { get; set; }

    [JsonPropertyName("nick")]
    public string? Uploader { get; set; }
}

public sealed class TokenResponse
{
    [JsonPropertyName("token")]
    public string? Token { get; set; }
}

public sealed class BridgeSearchResponse
{
    [JsonPropertyName("results")]
    public List<BridgeSubtitleResult> Results { get; set; } = new();
}

public sealed class BridgeSubtitleResult
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("title")]
    public string Title { get; set; } = string.Empty;

    [JsonPropertyName("comment")]
    public string? Comment { get; set; }

    [JsonPropertyName("downloadCount")]
    public int DownloadCount { get; set; }

    [JsonPropertyName("author")]
    public string? Author { get; set; }

    [JsonPropertyName("format")]
    public string? Format { get; set; }
}
