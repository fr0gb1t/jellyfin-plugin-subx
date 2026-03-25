using Jellyfin.Plugin.Subdivx.Configuration;
using Jellyfin.Plugin.Subdivx.Services;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Controller.Subtitles;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.Providers;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.Subdivx;

public sealed class SubdivxSubtitleProvider : ISubtitleProvider
{
    private readonly ILogger<SubdivxSubtitleProvider> _logger;
    private readonly IHttpClientFactory _httpClientFactory;

    public SubdivxSubtitleProvider(ILogger<SubdivxSubtitleProvider> logger, IHttpClientFactory httpClientFactory)
    {
        _logger = logger;
        _httpClientFactory = httpClientFactory;
    }

    public string Name => "Subdivx";

    public IEnumerable<VideoContentType> SupportedMediaTypes => new[] { VideoContentType.Movie, VideoContentType.Episode };

    public async Task<IEnumerable<RemoteSubtitleInfo>> Search(SubtitleSearchRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var config = Plugin.Instance?.Configuration ?? new PluginConfiguration();
        var client = new SubdivxClient(_httpClientFactory.CreateClient(nameof(SubdivxSubtitleProvider)), _logger);

        if (config.EnableDebugLogging)
        {
            _logger.LogInformation(
                "Subdivx provider search started. Name='{Name}', Series='{Series}', Year={Year}, Season={Season}, Episode={Episode}, Language='{Language}', TwoLetter='{TwoLetter}', MediaPath='{MediaPath}'",
                request.Name,
                request.SeriesName,
                request.ProductionYear,
                request.ParentIndexNumber,
                request.IndexNumber,
                request.Language,
                request.TwoLetterISOLanguageName,
                request.MediaPath);
        }

        try
        {
            IReadOnlyList<RemoteSubtitleInfo> results;
            if (config.UseBridge)
            {
                results = await client.SearchBridgeAsync(config, request, cancellationToken).ConfigureAwait(false);
            }
            else
            {
                results = await client.SearchDirectAsync(config, request, cancellationToken).ConfigureAwait(false);
            }

            if (config.EnableDebugLogging)
            {
                _logger.LogInformation("Subdivx provider search finished with {Count} result(s).", results.Count);
            }

            return results;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "Subdivx search failed. Name='{Name}', Series='{Series}', Year={Year}, Season={Season}, Episode={Episode}, Language='{Language}', TwoLetter='{TwoLetter}', MediaPath='{MediaPath}'",
                request.Name,
                request.SeriesName,
                request.ProductionYear,
                request.ParentIndexNumber,
                request.IndexNumber,
                request.Language,
                request.TwoLetterISOLanguageName,
                request.MediaPath);
            return Array.Empty<RemoteSubtitleInfo>();
        }
    }

    public async Task<SubtitleResponse> GetSubtitles(string id, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            throw new ArgumentException("Missing subtitle id.", nameof(id));
        }

        var config = Plugin.Instance?.Configuration ?? new PluginConfiguration();
        var client = new SubdivxClient(_httpClientFactory.CreateClient(nameof(SubdivxSubtitleProvider)), _logger);

        SubtitlePayload payload;
        if (id.StartsWith("bridge|", StringComparison.Ordinal))
        {
            payload = await client.DownloadBridgeAsync(config, id, cancellationToken).ConfigureAwait(false);
        }
        else
        {
            payload = await client.DownloadDirectAsync(config, id, cancellationToken).ConfigureAwait(false);
        }

        payload.Stream.Position = 0;
        return new SubtitleResponse
        {
            Format = payload.Format,
            Language = "spa",
            Stream = payload.Stream
        };
    }
}
