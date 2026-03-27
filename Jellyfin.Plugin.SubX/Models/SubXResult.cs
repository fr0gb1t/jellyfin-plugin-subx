using System.Text.Json.Serialization;

namespace Jellyfin.Plugin.SubX.Models;

public sealed class SubXSearchResponse
{
    [JsonPropertyName("aaData")]
    public List<SubXItem> Items { get; set; } = new();
}

public sealed class SubXItem
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
