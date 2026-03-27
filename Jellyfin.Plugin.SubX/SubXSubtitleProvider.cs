using Jellyfin.Plugin.SubX.Configuration;
using Jellyfin.Plugin.SubX.Services;
using MediaBrowser.Controller.Entities.Movies;
using MediaBrowser.Controller.Entities.TV;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Controller.Subtitles;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.Providers;
using Microsoft.Extensions.Logging;
using System.Text.RegularExpressions;

namespace Jellyfin.Plugin.SubX;

public sealed class SubXSubtitleProvider : ISubtitleProvider
{
    private readonly ILogger<SubXSubtitleProvider> _logger;
    private readonly ILibraryManager _libraryManager;

    public SubXSubtitleProvider(ILogger<SubXSubtitleProvider> logger, ILibraryManager libraryManager)
    {
        _logger = logger;
        _libraryManager = libraryManager;
    }

    public string Name => "SubX";

    public IEnumerable<VideoContentType> SupportedMediaTypes => new[] { VideoContentType.Movie, VideoContentType.Episode };

    public async Task<IEnumerable<RemoteSubtitleInfo>> Search(SubtitleSearchRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var config = Plugin.Instance?.Configuration ?? new PluginConfiguration();
        var client = new SubXClient(_logger);
        var queries = BuildQueries(request);

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
            var results = await client.SearchDirectAsync(config, request, queries, cancellationToken).ConfigureAwait(false);

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

    private List<string> BuildQueries(SubtitleSearchRequest request)
    {
        var queries = new List<string>();
        var item = !string.IsNullOrWhiteSpace(request.MediaPath)
            ? _libraryManager.FindByPath(request.MediaPath, false)
            : null;

        switch (item)
        {
            case Episode episode when episode.Season?.IndexNumber.HasValue == true && episode.IndexNumber.HasValue:
                AddEpisodeQueries(queries, episode);
                break;
            case Movie movie:
                AddMovieQueries(queries, movie);
                break;
        }

        if (queries.Count == 0)
        {
            queries.AddRange(SubXClient.BuildDefaultQueries(request));
        }
        else
        {
            foreach (var fallbackQuery in SubXClient.BuildDefaultQueries(request))
            {
                if (!queries.Contains(fallbackQuery, StringComparer.OrdinalIgnoreCase))
                {
                    queries.Add(fallbackQuery);
                }
            }
        }

        return queries;
    }

    private static void AddEpisodeQueries(List<string> queries, Episode episode)
    {
        var seasonNumber = episode.Season!.IndexNumber!.Value;
        var episodeNumber = episode.IndexNumber!.Value;

        foreach (var seriesName in GetCandidateNames(episode.Series?.Name, episode.Series?.OriginalTitle))
        {
            queries.Add($"{seriesName} S{seasonNumber:00}E{episodeNumber:00}");
            queries.Add($"{seriesName} {seasonNumber}x{episodeNumber:00}");
            queries.Add(seriesName);
        }

        var episodeFileName = Path.GetFileNameWithoutExtension(episode.Path);
        if (!string.IsNullOrWhiteSpace(episodeFileName))
        {
            queries.Add(NormalizeQuery(episodeFileName));
        }
    }

    private static void AddMovieQueries(List<string> queries, Movie movie)
    {
        foreach (var title in GetCandidateNames(movie.Name, movie.OriginalTitle))
        {
            if (movie.ProductionYear.HasValue)
            {
                queries.Add($"{title} {movie.ProductionYear.Value}");
            }

            queries.Add(title);
        }
    }

    private static IEnumerable<string> GetCandidateNames(params string?[] names)
    {
        return names
            .Select(x => NormalizeQuery(x ?? string.Empty))
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.OrdinalIgnoreCase);
    }

    private static string NormalizeQuery(string query)
    {
        return Regex.Replace(query, @"\s+", " ").Trim();
    }
}
