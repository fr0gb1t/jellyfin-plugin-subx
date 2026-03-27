using Jellyfin.Plugin.SubX.Configuration;
using Jellyfin.Plugin.SubX.Services;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Controller.Subtitles;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.Providers;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.SubX;

public sealed class SubXSubtitleProvider : ISubtitleProvider
{
    private readonly ILogger<SubXSubtitleProvider> _logger;

    public SubXSubtitleProvider(ILogger<SubXSubtitleProvider> logger)
    {
        _logger = logger;
    }

    public string Name => "SubX";

    public IEnumerable<VideoContentType> SupportedMediaTypes => new[] { VideoContentType.Movie, VideoContentType.Episode };

    public async Task<IEnumerable<RemoteSubtitleInfo>> Search(SubtitleSearchRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var config = Plugin.Instance?.Configuration ?? new PluginConfiguration();
        var client = new SubXClient(_logger);

        if (config.EnableDebugLogging)
        {
            _logger.LogInformation(
                "SubX provider search started. Name='{Name}', Series='{Series}', Year={Year}, Season={Season}, Episode={Episode}, Language='{Language}', TwoLetter='{TwoLetter}', MediaPath='{MediaPath}'",
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
            var results = await client.SearchDirectAsync(config, request, cancellationToken).ConfigureAwait(false);

            if (config.EnableDebugLogging)
            {
                _logger.LogInformation("SubX provider search finished with {Count} result(s).", results.Count);
            }

            return results;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "SubX search failed. Name='{Name}', Series='{Series}', Year={Year}, Season={Season}, Episode={Episode}, Language='{Language}', TwoLetter='{TwoLetter}', MediaPath='{MediaPath}'",
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
        var client = new SubXClient(_logger);
        var payload = await client.DownloadDirectAsync(config, id, cancellationToken).ConfigureAwait(false);

        payload.Stream.Position = 0;
        return new SubtitleResponse
        {
            Format = payload.Format,
            Language = "spa",
            Stream = payload.Stream
        };
    }
}
