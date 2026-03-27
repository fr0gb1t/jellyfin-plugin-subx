using System.Globalization;
using System.IO.Compression;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.RegularExpressions;
using Jellyfin.Plugin.SubX.Configuration;
using Jellyfin.Plugin.SubX.Models;
using MediaBrowser.Controller.Subtitles;
using MediaBrowser.Model.Providers;
using Microsoft.Extensions.Logging;
using SharpCompress.Archives;

namespace Jellyfin.Plugin.SubX.Services;

public sealed class SubXClient
{
    private static readonly Regex VersionRegex = new(@"(?:index-min\.js|sdx-min\.css)\?v=([0-9.]+)", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex HtmlTagRegex = new("<[^>]+>", RegexOptions.Compiled);
    private static readonly string[] SubtitleExtensions = [".srt", ".ass", ".ssa", ".sub"];

    private readonly ILogger _logger;
    private readonly JsonSerializerOptions _jsonOptions = new(JsonSerializerDefaults.Web);

    public SubXClient(ILogger logger)
    {
        _logger = logger;
    }

    public async Task<IReadOnlyList<RemoteSubtitleInfo>> SearchDirectAsync(PluginConfiguration config, SubtitleSearchRequest request, IReadOnlyList<string>? queries, CancellationToken cancellationToken)
    {
        using var httpClient = CreateHttpClient(config);

        var versionSuffix = await GetVersionSuffixAsync(httpClient, cancellationToken).ConfigureAwait(false);
        var token = await GetTokenAsync(httpClient, cancellationToken).ConfigureAwait(false);
        var searchField = $"buscar{versionSuffix}";
        var queryCandidates = queries?.Count > 0 ? queries : BuildDefaultQueries(request);
        if (queryCandidates.Count == 0)
        {
            return Array.Empty<RemoteSubtitleInfo>();
        }

        List<SubXItem>? items = null;
        string? selectedQuery = null;
        foreach (var query in queryCandidates)
        {
            using var content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["tabla"] = "resultados",
                ["filtros"] = string.Empty,
                [searchField] = query,
                ["token"] = token
            });

            using var response = await httpClient.PostAsync("https://subdivx.com/inc/ajax.php", content, cancellationToken).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();
            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
            var payload = await JsonSerializer.DeserializeAsync<SubXSearchResponse>(stream, _jsonOptions, cancellationToken).ConfigureAwait(false);
            var count = payload?.Items?.Count ?? 0;

            if (config.EnableDebugLogging)
            {
                _logger.LogInformation("SubX direct search '{Query}' returned {Count} results.", query, count);
            }

            if (count > 0)
            {
                items = payload!.Items;
                selectedQuery = query;
                break;
            }
        }

        if (items is null || items.Count == 0 || string.IsNullOrWhiteSpace(selectedQuery))
        {
            if (config.EnableDebugLogging)
            {
                _logger.LogInformation("SubX direct search produced no results for any query candidate.");
            }

            return Array.Empty<RemoteSubtitleInfo>();
        }

        if (config.EnableDebugLogging)
        {
            _logger.LogInformation("SubX direct search selected query '{Query}' for ranking.", selectedQuery);
        }

        var ranked = items
            .Select(item => new { Item = item, Score = ScoreItem(item, request, selectedQuery) })
            .OrderByDescending(x => x.Score)
            .ThenByDescending(x => x.Item.DownloadCount)
            .Take(20)
            .Select(x => ToRemoteSubtitleInfo(x.Item))
            .ToList();

        return ranked;
    }

    public async Task<SubtitlePayload> DownloadDirectAsync(PluginConfiguration config, string providerId, CancellationToken cancellationToken)
    {
        using var httpClient = CreateHttpClient(config);

        var subtitleId = ParseSubXProviderId(providerId);
        using var response = await httpClient.GetAsync($"https://subdivx.com/descargar.php?f=1&id={subtitleId}", cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

        var bytes = await response.Content.ReadAsByteArrayAsync(cancellationToken).ConfigureAwait(false);
        var mediaType = response.Content.Headers.ContentType?.MediaType;
        var fileName = response.Content.Headers.ContentDisposition?.FileNameStar ?? response.Content.Headers.ContentDisposition?.FileName;
        return await ExtractSubtitleAsync(bytes, mediaType, fileName).ConfigureAwait(false);
    }

    private static HttpClient CreateHttpClient(PluginConfiguration config)
    {
        var httpClient = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(60)
        };
        httpClient.DefaultRequestHeaders.Accept.Clear();
        httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("text/javascript"));
        httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("*/*", 0.1));
        httpClient.DefaultRequestHeaders.Referrer = new Uri("https://subdivx.com/");
        httpClient.DefaultRequestHeaders.Add("Origin", "https://subdivx.com");
        httpClient.DefaultRequestHeaders.Add("X-Requested-With", "XMLHttpRequest");
        httpClient.DefaultRequestHeaders.UserAgent.ParseAdd(config.UserAgent);

        var cookieHeader = BuildCookieHeader(config);
        if (!string.IsNullOrWhiteSpace(cookieHeader))
        {
            httpClient.DefaultRequestHeaders.Add("Cookie", cookieHeader);
        }

        return httpClient;
    }

    private static string BuildCookieHeader(PluginConfiguration config)
    {
        return config.CookieHeader.Trim();
    }

    private async Task<string> GetVersionSuffixAsync(HttpClient httpClient, CancellationToken cancellationToken)
    {
        var html = await httpClient.GetStringAsync("https://subdivx.com/", cancellationToken).ConfigureAwait(false);
        var match = VersionRegex.Match(html);
        if (!match.Success)
        {
            throw new InvalidOperationException("Unable to determine current Subdivx frontend version.");
        }

        var version = match.Groups[1].Value.Replace(".", string.Empty);
        if (!string.IsNullOrWhiteSpace(version))
        {
            return version;
        }

        throw new InvalidOperationException("Subdivx frontend version was empty.");
    }

    private async Task<string> GetTokenAsync(HttpClient httpClient, CancellationToken cancellationToken)
    {
        await using var stream = await httpClient.GetStreamAsync("https://subdivx.com/inc/gt.php?gt=1", cancellationToken).ConfigureAwait(false);
        var token = await JsonSerializer.DeserializeAsync<TokenResponse>(stream, _jsonOptions, cancellationToken).ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(token?.Token))
        {
            throw new InvalidOperationException("Subdivx token response did not contain a token.");
        }

        return token.Token;
    }

    public static List<string> BuildDefaultQueries(SubtitleSearchRequest request)
    {
        var queries = new List<string>();

        if (IsEpisodeRequest(request))
        {
            var seriesName = request.SeriesName ?? request.Name ?? string.Empty;
            if (!string.IsNullOrWhiteSpace(seriesName) && request.ParentIndexNumber.HasValue && request.IndexNumber.HasValue)
            {
                queries.Add($"{seriesName} S{request.ParentIndexNumber.Value:00}E{request.IndexNumber.Value:00}");
                queries.Add($"{seriesName} {request.ParentIndexNumber.Value}x{request.IndexNumber.Value:00}");
            }
        }

        if (!string.IsNullOrWhiteSpace(request.Name) && request.ProductionYear.HasValue)
        {
            queries.Add($"{request.Name} {request.ProductionYear.Value}");
        }

        if (!string.IsNullOrWhiteSpace(request.Name))
        {
            queries.Add(request.Name);
        }

        if (!string.IsNullOrWhiteSpace(request.SeriesName))
        {
            queries.Add(request.SeriesName);
        }

        if (!string.IsNullOrWhiteSpace(request.MediaPath))
        {
            var fileName = Path.GetFileNameWithoutExtension(request.MediaPath);
            if (!string.IsNullOrWhiteSpace(fileName))
            {
                queries.Add(fileName);
            }
        }

        return queries
            .Select(NormalizeQuery)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static string NormalizeQuery(string query)
    {
        return Regex.Replace(query, @"\s+", " ").Trim();
    }

    private static int ScoreItem(SubXItem item, SubtitleSearchRequest request, string query)
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

        if (IsEpisodeRequest(request) && request.ParentIndexNumber.HasValue && request.IndexNumber.HasValue)
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

    private static RemoteSubtitleInfo ToRemoteSubtitleInfo(SubXItem item)
    {
        var uploaded = DateTimeOffset.TryParse(item.UploadedAt, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var parsed)
            ? parsed
            : DateTimeOffset.UtcNow;

        var format = string.IsNullOrWhiteSpace(item.Format) ? "srt" : item.Format!.Trim().ToLowerInvariant();
        return new RemoteSubtitleInfo
        {
            Id = $"subx|{item.Id}",
            Name = item.Title ?? $"SubX #{item.Id}",
            Author = item.Uploader,
            Comment = StripHtml(item.Description),
            DownloadCount = item.DownloadCount,
            ProviderName = "SubX",
            ThreeLetterISOLanguageName = "spa",
            Format = format,
            DateCreated = uploaded.UtcDateTime
        };
    }

    private static bool IsEpisodeRequest(SubtitleSearchRequest request)
    {
        return request.ParentIndexNumber.HasValue
            && request.IndexNumber.HasValue
            && (!string.IsNullOrWhiteSpace(request.SeriesName) || !string.IsNullOrWhiteSpace(request.Name));
    }

    private static string StripHtml(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        return HtmlTagRegex.Replace(value, string.Empty);
    }

    private static long ParseSubXProviderId(string providerId)
    {
        var parts = providerId.Split('|', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length != 2 || !long.TryParse(parts[1], out var id))
        {
            throw new FormatException($"Invalid SubX provider id: {providerId}");
        }

        return id;
    }

    private static async Task<SubtitlePayload> ExtractSubtitleAsync(byte[] bytes, string? mediaType, string? fileName)
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

        return await ExtractFromArchiveAsync(bytes).ConfigureAwait(false);
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

    private static Task<SubtitlePayload> ExtractFromArchiveAsync(byte[] bytes)
    {
        using var memory = new MemoryStream(bytes);
        using var archive = ArchiveFactory.Open(memory);
        var candidate = archive.Entries
            .Where(x => !x.IsDirectory && !string.IsNullOrWhiteSpace(x.Key) && SubtitleExtensions.Contains(Path.GetExtension(x.Key).ToLowerInvariant()))
            .OrderBy(x => SubtitleExtensions.ToList().IndexOf(Path.GetExtension(x.Key!).ToLowerInvariant()))
            .ThenByDescending(x => x.Size)
            .FirstOrDefault();

        if (candidate is null)
        {
            throw new InvalidOperationException("The downloaded archive does not contain a supported subtitle file.");
        }

        var output = new MemoryStream();
        using var entryStream = candidate.OpenEntryStream();
        entryStream.CopyTo(output);
        output.Position = 0;
        return Task.FromResult(new SubtitlePayload(Path.GetExtension(candidate.Key!).TrimStart('.'), output));
    }
}

public sealed record SubtitlePayload(string Format, MemoryStream Stream);
