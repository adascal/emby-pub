using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediaBrowser.Common.Net;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.Movies;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.Logging;
using MediaBrowser.Model.Providers;
using Kinopub.Plugin.Api;

namespace Kinopub.Plugin.Providers
{
    public class KinopubMovieProvider : IRemoteMetadataProvider<Movie, MovieInfo>, IHasOrder
    {
        private readonly IHttpClient _httpClient;
        private readonly ILogger _logger;

        public string Name => "Kinopub";
        public int Order => 2; // Lower priority than mainstream providers

        public KinopubMovieProvider(IHttpClient httpClient, ILogManager logManager)
        {
            _httpClient = httpClient;
            _logger = logManager.GetLogger(GetType().Name);
        }

        public async Task<IEnumerable<RemoteSearchResult>> GetSearchResults(MovieInfo searchInfo, CancellationToken cancellationToken)
        {
            var apiClient = Plugin.Instance?.GetApiClient();
            if (apiClient == null)
            {
                return Enumerable.Empty<RemoteSearchResult>();
            }

            try
            {
                // Try searching by Kinopub ID first
                var kinopubId = searchInfo.GetProviderId("Kinopub");
                if (!string.IsNullOrEmpty(kinopubId))
                {
                    var itemResponse = await apiClient.GetItemMediaAsync(kinopubId, cancellationToken);
                    if (itemResponse?.Item != null)
                    {
                        return new[] { ConvertToSearchResult(itemResponse.Item) };
                    }
                }

                // Fall back to search by name
                var searchQuery = searchInfo.Name;
                if (string.IsNullOrEmpty(searchQuery))
                {
                    return Enumerable.Empty<RemoteSearchResult>();
                }

                var searchResponse = await apiClient.SearchItemsAsync(searchQuery, "movie", 1, 10, cancellationToken);

                return searchResponse.Items
                    .Where(item => item.Type == "movie" || item.Type == "3d")
                    .Select(ConvertToSearchResult);
            }
            catch (Exception ex)
            {
                _logger.Error($"Error searching for movie: {ex.Message}");
                return Enumerable.Empty<RemoteSearchResult>();
            }
        }

        public async Task<MetadataResult<Movie>> GetMetadata(MovieInfo info, CancellationToken cancellationToken)
        {
            var result = new MetadataResult<Movie>();

            var apiClient = Plugin.Instance?.GetApiClient();
            if (apiClient == null)
            {
                return result;
            }

            try
            {
                var kinopubId = info.GetProviderId("Kinopub");
                if (string.IsNullOrEmpty(kinopubId))
                {
                    // Try to find by search
                    var searchResults = await GetSearchResults(info, cancellationToken);
                    var firstResult = searchResults.FirstOrDefault();

                    if (firstResult != null)
                    {
                        kinopubId = firstResult.GetProviderId("Kinopub");
                    }
                }

                if (string.IsNullOrEmpty(kinopubId))
                {
                    return result;
                }

                var itemResponse = await apiClient.GetItemMediaAsync(kinopubId, cancellationToken);
                var item = itemResponse?.Item;

                if (item == null)
                {
                    return result;
                }

                var movie = new Movie
                {
                    Name = item.Title,
                    OriginalTitle = item.Title,
                    Overview = item.Plot,
                    ProductionYear = item.Year,
                    PremiereDate = new DateTime(item.Year, 1, 1),
                    CommunityRating = (float)(item.ImdbRating > 0 ? item.ImdbRating : item.KinopoiskRating),
                    OfficialRating = "R" // Default rating
                };

                // Add provider IDs
                movie.SetProviderId("Kinopub", item.Id);
                if (item.Imdb > 0)
                {
                    movie.SetProviderId(MetadataProviders.Imdb, $"tt{item.Imdb:D7}");
                }

                // Add genres
                if (item.Genres != null && item.Genres.Count > 0)
                {
                    movie.Genres = item.Genres.Select(g => g.Title).ToArray();
                }

                // Add studios/countries
                if (item.Countries != null && item.Countries.Count > 0)
                {
                    movie.Studios = item.Countries.Select(c => c.Title).ToArray();
                }

                // Add people
                var people = new List<PersonInfo>();

                // Directors
                if (!string.IsNullOrEmpty(item.Director))
                {
                    people.AddRange(item.Director.Split(',')
                        .Select(name => new PersonInfo
                        {
                            Name = name.Trim(),
                            Type = PersonType.Director
                        }));
                }

                // Cast
                if (!string.IsNullOrEmpty(item.Cast))
                {
                    people.AddRange(item.Cast.Split(',')
                        .Select(name => new PersonInfo
                        {
                            Name = name.Trim(),
                            Type = PersonType.Actor
                        }));
                }

                result.Item = movie;
                result.HasMetadata = true;
                result.People = people;

                return result;
            }
            catch (Exception ex)
            {
                _logger.Error($"Error fetching metadata for movie: {ex.Message}");
                return result;
            }
        }

        private RemoteSearchResult ConvertToSearchResult(Models.Item item)
        {
            var result = new RemoteSearchResult
            {
                Name = item.Title,
                SearchProviderName = Name,
                ProductionYear = item.Year,
                Overview = item.Plot,
                ImageUrl = item.Posters?.Big
            };

            result.SetProviderId("Kinopub", item.Id);
            if (item.Imdb > 0)
            {
                result.SetProviderId(MetadataProviders.Imdb, $"tt{item.Imdb:D7}");
            }

            return result;
        }

        public Task<HttpResponseInfo> GetImageResponse(string url, CancellationToken cancellationToken)
        {
            return _httpClient.GetResponse(new HttpRequestOptions
            {
                Url = url,
                CancellationToken = cancellationToken
            });
        }
    }
}
