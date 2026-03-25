using System.Globalization;
using System.IO.Compression;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;
using Jellyfin.Plugin.Subdivx.Configuration;
using Jellyfin.Plugin.Subdivx.Models;
using MediaBrowser.Controller.Subtitles;
using MediaBrowser.Model.Providers;
using Microsoft.Extensions.Logging;
using SharpCompress.Archives;
using SharpCompress.Common;

namespace Jellyfin.Plugin.Subdivx.Services;

public sealed class SubdivxClient
{
    private static readonly Regex VersionRegex = new(@"(?:index-min\.js|sdx-min\.css)\?v=([0-9.]+)", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex HtmlTagRegex = new("<[^>]+>", RegexOptions.Compiled);
    private static readonly string[] SubtitleExtensions = [".srt", ".ass", ".ssa", ".sub"];

    private readonly HttpClient _httpClient;
    private readonly ILogger _logger;
    private readonly JsonSerializerOptions _jsonOptions = new(JsonSerializerDefaults.Web);

    public SubdivxClient(HttpClient httpClient, ILogger logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task<IReadOnlyList<RemoteSubtitleInfo>> SearchDirectAsync(PluginConfiguration config, SubtitleSearchRequest request, CancellationToken cancellationToken)
    {
        if (config.OnlySpanish && !IsSpanishRequest(request))
        {
            return Array.Empty<RemoteSubtitleInfo>();
        }

        ConfigureDefaultHeaders(config);

        var versionSuffix = await GetVersionSuffixAsync(cancellationToken).ConfigureAwait(false);
        var token = await GetTokenAsync(cancellationToken).ConfigureAwait(false);
        var searchField = $"buscar{versionSuffix}";
        var query = BuildQuery(request);

        if (string.IsNullOrWhiteSpace(query))
        {
            return Array.Empty<RemoteSubtitleInfo>();
        }

        using var content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["tabla"] = "resultados",
            ["filtros"] = string.Empty,
            [searchField] = query,
            ["token"] = token
        });

        using var response = await _httpClient.PostAsync("https://subdivx.com/inc/ajax.php", content, cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        var payload = await JsonSerializer.DeserializeAsync<SubdivxSearchResponse>(stream, _jsonOptions, cancellationToken).ConfigureAwait(false);
        if (payload?.Items is null || payload.Items.Count == 0)
        {
            return Array.Empty<RemoteSubtitleInfo>();
        }

        var ranked = payload.Items
            .Select(item => new { Item = item, Score = ScoreItem(item, request, query) })
            .OrderByDescending(x => x.Score)
            .ThenByDescending(x => x.Item.DownloadCount)
            .Take(20)
            .Select(x => ToRemoteSubtitleInfo(x.Item))
            .ToList();

        return ranked;
    }

    public async Task<SubtitlePayload> DownloadDirectAsync(PluginConfiguration config, string providerId, CancellationToken cancellationToken)
    {
        ConfigureDefaultHeaders(config);

        var subtitleId = ParseSubdivxProviderId(providerId);
        using var response = await _httpClient.GetAsync($"https://subdivx.com/descargar.php?f=1&id={subtitleId}", cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

        var bytes = await response.Content.ReadAsByteArrayAsync(cancellationToken).ConfigureAwait(false);
        var mediaType = response.Content.Headers.ContentType?.MediaType;
        var fileName = response.Content.Headers.ContentDisposition?.FileNameStar ?? response.Content.Headers.ContentDisposition?.FileName;
        return await ExtractSubtitleAsync(bytes, mediaType, fileName, cancellationToken).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<RemoteSubtitleInfo>> SearchBridgeAsync(PluginConfiguration config, SubtitleSearchRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(config.BridgeBaseUrl))
        {
            return Array.Empty<RemoteSubtitleInfo>();
        }

        var endpoint = config.BridgeBaseUrl.TrimEnd('/') + "/search";
        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, endpoint)
        {
            Content = JsonContent.Create(new
            {
                contentType = request.ContentType.ToString(),
                name = request.Name,
                seriesName = request.SeriesName,
                seasonNumber = request.ParentIndexNumber,
                episodeNumber = request.IndexNumber,
                year = request.ProductionYear,
                mediaPath = request.MediaPath,
                language = request.Language,
                twoLetterLanguage = request.TwoLetterISOLanguageName
            })
        };

        if (!string.IsNullOrWhiteSpace(config.BridgeApiKey))
        {
            httpRequest.Headers.Add("X-Api-Key", config.BridgeApiKey);
        }

        using var response = await _httpClient.SendAsync(httpRequest, cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        var payload = await JsonSerializer.DeserializeAsync<BridgeSearchResponse>(stream, _jsonOptions, cancellationToken).ConfigureAwait(false);
        if (payload?.Results is null)
        {
            return Array.Empty<RemoteSubtitleInfo>();
        }

        return payload.Results.Select(x => new RemoteSubtitleInfo
        {
            Id = $"bridge|{x.Id}",
            Name = x.Title,
            Author = x.Author,
            Comment = x.Comment,
            DownloadCount = x.DownloadCount,
            ProviderName = "Subdivx",
            ThreeLetterISOLanguageName = "spa",
            Format = string.IsNullOrWhiteSpace(x.Format) ? "srt" : x.Format,
            DateCreated = DateTimeOffset.UtcNow
        }).ToList();
    }

    public async Task<SubtitlePayload> DownloadBridgeAsync(PluginConfiguration config, string providerId, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(config.BridgeBaseUrl))
        {
            throw new InvalidOperationException("BridgeBaseUrl is empty.");
        }

        var bridgeId = providerId.Substring("bridge|".Length, StringComparison.Ordinal);
        var endpoint = config.BridgeBaseUrl.TrimEnd('/') + "/download/" + Uri.EscapeDataString(bridgeId);
        using var request = new HttpRequestMessage(HttpMethod.Get, endpoint);
        if (!string.IsNullOrWhiteSpace(config.BridgeApiKey))
        {
            request.Headers.Add("X-Api-Key", config.BridgeApiKey);
        }

        using var response = await _httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        var bytes = await response.Content.ReadAsByteArrayAsync(cancellationToken).ConfigureAwait(false);
        return await ExtractSubtitleAsync(bytes, response.Content.Headers.ContentType?.MediaType, response.Content.Headers.ContentDisposition?.FileNameStar, cancellationToken).ConfigureAwait(false);
    }

    private void ConfigureDefaultHeaders(PluginConfiguration config)
    {
        _httpClient.DefaultRequestHeaders.Clear();
        _httpClient.DefaultRequestHeaders.Accept.Clear();
        _httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        _httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("text/javascript"));
        _httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("*/*", 0.1));
        _httpClient.DefaultRequestHeaders.Referrer = new Uri("https://subdivx.com/");
        _httpClient.DefaultRequestHeaders.Add("Origin", "https://subdivx.com");
        _httpClient.DefaultRequestHeaders.Add("X-Requested-With", "XMLHttpRequest");
        _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd(config.UserAgent);

        var cookieHeader = BuildCookieHeader(config);
        if (!string.IsNullOrWhiteSpace(cookieHeader))
        {
            _httpClient.DefaultRequestHeaders.Add("Cookie", cookieHeader);
        }
    }

    private static string BuildCookieHeader(PluginConfiguration config)
    {
        if (!string.IsNullOrWhiteSpace(config.CookieHeader))
        {
            return config.CookieHeader.Trim();
        }

        var parts = new List<string>();
        if (!string.IsNullOrWhiteSpace(config.CfClearance))
        {
            parts.Add($"cf_clearance={config.CfClearance.Trim()}");
        }

        if (!string.IsNullOrWhiteSpace(config.SdxCookie))
        {
            parts.Add($"sdx={config.SdxCookie.Trim()}");
        }

        return string.Join("; ", parts);
    }

    private async Task<string> GetVersionSuffixAsync(CancellationToken cancellationToken)
    {
        var html = await _httpClient.GetStringAsync("https://subdivx.com/", cancellationToken).ConfigureAwait(false);
        var match = VersionRegex.Match(html);
        if (!match.Success)
        {
            throw new InvalidOperationException("Unable to determine current Subdivx frontend version.");
        }

        return match.Groups[1].Value.Replace(".", string.Empty, StringComparison.Ordinal);
    }

    private async Task<string> GetTokenAsync(CancellationToken cancellationToken)
    {
        await using var stream = await _httpClient.GetStreamAsync("https://subdivx.com/inc/gt.php?gt=1", cancellationToken).ConfigureAwait(false);
        var token = await JsonSerializer.DeserializeAsync<TokenResponse>(stream, _jsonOptions, cancellationToken).ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(token?.Token))
        {
            throw new InvalidOperationException("Subdivx token response did not contain a token.");
        }

        return token.Token;
    }

    private static string BuildQuery(SubtitleSearchRequest request)
    {
        if (!string.IsNullOrWhiteSpace(request.MediaPath))
        {
            var fileName = Path.GetFileNameWithoutExtension(request.MediaPath);
            if (!string.IsNullOrWhiteSpace(fileName))
            {
                return fileName;
            }
        }

        if (request.ContentType == MediaBrowser.Model.Entities.VideoContentType.Episode)
        {
            var seriesName = request.SeriesName ?? request.Name ?? string.Empty;
            if (!string.IsNullOrWhiteSpace(seriesName) && request.ParentIndexNumber.HasValue && request.IndexNumber.HasValue)
            {
                return $"{seriesName} S{request.ParentIndexNumber.Value:00}E{request.IndexNumber.Value:00}";
            }
        }

        if (!string.IsNullOrWhiteSpace(request.Name) && request.ProductionYear.HasValue)
        {
            return $"{request.Name} {request.ProductionYear.Value}";
        }

        return request.Name ?? request.SeriesName ?? string.Empty;
    }

    private static bool IsSpanishRequest(SubtitleSearchRequest request)
    {
        var lang3 = request.Language ?? string.Empty;
        var lang2 = request.TwoLetterISOLanguageName ?? string.Empty;
        return lang3.Equals("spa", StringComparison.OrdinalIgnoreCase)
            || lang2.Equals("es", StringComparison.OrdinalIgnoreCase)
            || string.IsNullOrWhiteSpace(lang3);
    }

    private static int ScoreItem(SubdivxItem item, SubtitleSearchRequest request, string query)
    {
        var haystack = $"{item.Title} {item.Description}".ToLowerInvariant();
        var score = 0;
        foreach (var token in query.ToLowerInvariant().Split([' ', '.', '-', '_'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Distinct())
        {
            if (haystack.Contains(token, StringComparison.Ordinal))
            {
                score += 10;
            }
        }

        if (request.ContentType == MediaBrowser.Model.Entities.VideoContentType.Episode && request.ParentIndexNumber.HasValue && request.IndexNumber.HasValue)
        {
            var episodeTag = $"s{request.ParentIndexNumber.Value:00}e{request.IndexNumber.Value:00}";
            if (haystack.Contains(episodeTag, StringComparison.OrdinalIgnoreCase))
            {
                score += 100;
            }
        }

        if (request.ProductionYear.HasValue && haystack.Contains(request.ProductionYear.Value.ToString(CultureInfo.InvariantCulture), StringComparison.Ordinal))
        {
            score += 40;
        }

        score += Math.Min(item.DownloadCount / 25, 50);
        score += Math.Min(item.CommentCount * 2, 10);
        return score;
    }

    private static RemoteSubtitleInfo ToRemoteSubtitleInfo(SubdivxItem item)
    {
        var uploaded = DateTimeOffset.TryParse(item.UploadedAt, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var parsed)
            ? parsed
            : DateTimeOffset.UtcNow;

        var format = string.IsNullOrWhiteSpace(item.Format) ? "srt" : item.Format!.Trim().ToLowerInvariant();
        return new RemoteSubtitleInfo
        {
            Id = $"subdivx|{item.Id}",
            Name = item.Title ?? $"Subdivx #{item.Id}",
            Author = item.Uploader,
            Comment = StripHtml(item.Description),
            DownloadCount = item.DownloadCount,
            ProviderName = "Subdivx",
            ThreeLetterISOLanguageName = "spa",
            Format = format,
            DateCreated = uploaded
        };
    }

    private static string StripHtml(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        return HtmlTagRegex.Replace(value, string.Empty);
    }

    private static long ParseSubdivxProviderId(string providerId)
    {
        var parts = providerId.Split('|', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length != 2 || !long.TryParse(parts[1], out var id))
        {
            throw new FormatException($"Invalid Subdivx provider id: {providerId}");
        }

        return id;
    }

    private static async Task<SubtitlePayload> ExtractSubtitleAsync(byte[] bytes, string? mediaType, string? fileName, CancellationToken cancellationToken)
    {
        var normalizedName = (fileName ?? string.Empty).Trim('"');
        var extension = Path.GetExtension(normalizedName).ToLowerInvariant();
        if (SubtitleExtensions.Contains(extension))
        {
            return new SubtitlePayload(extension.TrimStart('.'), new MemoryStream(bytes));
        }

        if (extension == ".zip" || string.Equals(mediaType, "application/zip", StringComparison.OrdinalIgnoreCase))
        {
            return ExtractFromZip(bytes);
        }

        return await ExtractFromArchiveAsync(bytes, cancellationToken).ConfigureAwait(false);
    }

    private static SubtitlePayload ExtractFromZip(byte[] bytes)
    {
        using var memory = new MemoryStream(bytes);
        using var archive = new ZipArchive(memory, ZipArchiveMode.Read, leaveOpen: false);
        var entry = archive.Entries
            .Where(x => SubtitleExtensions.Contains(Path.GetExtension(x.FullName).ToLowerInvariant()))
            .OrderBy(x => SubtitleExtensions.ToList().IndexOf(Path.GetExtension(x.FullName).ToLowerInvariant()))
            .ThenByDescending(x => x.Length)
            .FirstOrDefault();

        if (entry is null)
        {
            throw new InvalidOperationException("The ZIP archive does not contain a supported subtitle file.");
        }

        var output = new MemoryStream();
        using var input = entry.Open();
        input.CopyTo(output);
        output.Position = 0;
        return new SubtitlePayload(Path.GetExtension(entry.FullName).TrimStart('.'), output);
    }

    private static Task<SubtitlePayload> ExtractFromArchiveAsync(byte[] bytes, CancellationToken cancellationToken)
    {
        using var memory = new MemoryStream(bytes);
        using var archive = ArchiveFactory.Open(memory);
        var candidate = archive.Entries
            .Where(x => !x.IsDirectory && SubtitleExtensions.Contains(Path.GetExtension(x.Key).ToLowerInvariant()))
            .OrderBy(x => SubtitleExtensions.ToList().IndexOf(Path.GetExtension(x.Key).ToLowerInvariant()))
            .ThenByDescending(x => x.Size)
            .FirstOrDefault();

        if (candidate is null)
        {
            throw new InvalidOperationException("The downloaded archive does not contain a supported subtitle file.");
        }

        var output = new MemoryStream();
        candidate.WriteTo(output, new ExtractionOptions { ExtractFullPath = false, Overwrite = true });
        output.Position = 0;
        return Task.FromResult(new SubtitlePayload(Path.GetExtension(candidate.Key).TrimStart('.'), output));
    }
}

public sealed record SubtitlePayload(string Format, MemoryStream Stream);
