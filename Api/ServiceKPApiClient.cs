using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using System.Web;
using MediaBrowser.Common.Net;
using MediaBrowser.Model.Logging;
using ServiceKP.Plugin.Models;

namespace ServiceKP.Plugin.Api
{
    public class ServiceKPApiClient
    {
        private readonly IHttpClient _httpClient;
        private readonly ILogger _logger;
        private readonly string _clientId;
        private readonly string _clientSecret;
        private readonly string _baseUrl;
        private readonly SimpleCache _cache;

        private string? _accessToken;
        private string? _refreshToken;
        private DateTime _tokenExpiry;
        private readonly SemaphoreSlim _tokenRefreshLock = new(1, 1);
        private Action<string, string, DateTime>? _onTokensRefreshed;

        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            PropertyNameCaseInsensitive = true
        };

        public ServiceKPApiClient(IHttpClient httpClient, ILogger logger, string baseUrl, string clientId, string clientSecret)
        {
            _httpClient = httpClient;
            _logger = logger;
            _baseUrl = baseUrl;
            _clientId = clientId;
            _clientSecret = clientSecret;
            _cache = new SimpleCache(TimeSpan.FromMinutes(5));
        }

        public void SetTokensRefreshedCallback(Action<string, string, DateTime> callback)
        {
            _onTokensRefreshed = callback;
        }

        public void SetTokens(string accessToken, string refreshToken, DateTime expiry)
        {
            _accessToken = accessToken;
            _refreshToken = refreshToken;
            _tokenExpiry = expiry;
        }

        public string? GetAccessToken() => _accessToken;
        public string? GetRefreshToken() => _refreshToken;

        public void ClearTokens()
        {
            _accessToken = null;
            _refreshToken = null;
            _tokenExpiry = DateTime.MinValue;
        }

        private async Task<T> GetOrRefreshAccessToken<T>(Func<string?, Task<T>> action)
        {
            if (string.IsNullOrEmpty(_accessToken) || DateTime.UtcNow >= _tokenExpiry)
            {
                if (!string.IsNullOrEmpty(_refreshToken))
                {
                    await _tokenRefreshLock.WaitAsync();
                    try
                    {
                        if (string.IsNullOrEmpty(_accessToken) || DateTime.UtcNow >= _tokenExpiry)
                        {
                            await RefreshTokensAsync(_refreshToken);
                        }
                    }
                    finally
                    {
                        _tokenRefreshLock.Release();
                    }
                }
            }

            return await action(_accessToken);
        }

        private string BuildQueryString(Dictionary<string, string> parameters)
        {
            var query = HttpUtility.ParseQueryString(string.Empty);
            foreach (var param in parameters.Where(p => !string.IsNullOrEmpty(p.Value)))
            {
                query[param.Key] = param.Value;
            }
            return query.ToString() ?? string.Empty;
        }

        private async Task<T> RequestAsync<T>(string method, string endpoint, Dictionary<string, string>? queryParams = null, Dictionary<string, string>? formData = null, CancellationToken cancellationToken = default) where T : class
        {
            queryParams ??= new Dictionary<string, string>();

            // Add access token to query if not requesting tokens
            if (!queryParams.ContainsKey("grant_type") && !string.IsNullOrEmpty(_accessToken))
            {
                queryParams["access_token"] = _accessToken;
            }

            var url = $"{_baseUrl}{endpoint}?{BuildQueryString(queryParams)}";

            try
            {
                HttpResponseInfo response;

                if (method == "GET")
                {
                    response = await _httpClient.GetResponse(new HttpRequestOptions
                    {
                        Url = url,
                        CancellationToken = cancellationToken,
                        LogErrorResponseBody = true
                    });
                }
                else // POST
                {
                    var requestOptions = new HttpRequestOptions
                    {
                        Url = url,
                        CancellationToken = cancellationToken,
                        LogErrorResponseBody = true
                    };

                    if (formData != null)
                    {
                        requestOptions.SetPostData(formData);
                    }

                    response = await _httpClient.Post(requestOptions);
                }

                using (response.Content)
                {
                    var json = await response.Content.ReadAsStringAsync();
                    _logger.Debug($"API Response: {json}");

                    var result = JsonSerializer.Deserialize<T>(json, JsonOptions);

                    // Check for 401 and clear tokens
                    if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
                    {
                        ClearTokens();
                    }

                    return result ?? throw new Exception("Failed to deserialize response");
                }
            }
            catch (Exception ex)
            {
                _logger.Error($"API request failed: {ex.Message}");
                throw;
            }
        }

        private Task<T> GetAsync<T>(string endpoint, Dictionary<string, string>? queryParams = null, CancellationToken cancellationToken = default) where T : class
        {
            return GetOrRefreshAccessToken(token => RequestAsync<T>("GET", endpoint, queryParams, null, cancellationToken));
        }

        private Task<T> PostAsync<T>(string endpoint, Dictionary<string, string>? formData = null, Dictionary<string, string>? queryParams = null, CancellationToken cancellationToken = default) where T : class
        {
            return GetOrRefreshAccessToken(token => RequestAsync<T>("POST", endpoint, queryParams, formData, cancellationToken));
        }

        // Authentication Methods

        public async Task<DeviceCodeResponse> RequestDeviceCodeAsync(CancellationToken cancellationToken = default)
        {
            var formData = new Dictionary<string, string>
            {
                ["grant_type"] = "device_code",
                ["client_id"] = _clientId,
                ["client_secret"] = _clientSecret
            };

            return await RequestAsync<DeviceCodeResponse>("POST", "/oauth2/device", formData, null, cancellationToken);
        }

        public async Task<TokensResponse> RequestTokensAsync(string code, CancellationToken cancellationToken = default)
        {
            var formData = new Dictionary<string, string>
            {
                ["grant_type"] = "device_token",
                ["client_id"] = _clientId,
                ["client_secret"] = _clientSecret,
                ["code"] = code
            };

            return await RequestAsync<TokensResponse>("POST", "/oauth2/device", formData, null, cancellationToken);
        }

        public async Task<TokensResponse> RefreshTokensAsync(string refreshToken, CancellationToken cancellationToken = default)
        {
            var formData = new Dictionary<string, string>
            {
                ["grant_type"] = "refresh_token",
                ["client_id"] = _clientId,
                ["client_secret"] = _clientSecret,
                ["refresh_token"] = refreshToken
            };

            var response = await RequestAsync<TokensResponse>("POST", "/oauth2/token", formData, null, cancellationToken);

            if (response.Error == null && !string.IsNullOrEmpty(response.AccessToken))
            {
                _accessToken = response.AccessToken;
                _refreshToken = response.RefreshToken;
                _tokenExpiry = DateTime.UtcNow.AddSeconds(response.ExpiresIn);

                // Notify plugin to save refreshed tokens
                _onTokensRefreshed?.Invoke(_accessToken, _refreshToken, _tokenExpiry);
                _logger.Info("Access token refreshed and saved successfully");
            }

            return response;
        }

        public Task<UserResponse> GetUserAsync(CancellationToken cancellationToken = default)
        {
            return GetAsync<UserResponse>("/v1/user", null, cancellationToken);
        }

        public async Task DeviceNotifyAsync(DeviceInfo deviceInfo, CancellationToken cancellationToken = default)
        {
            var formData = new Dictionary<string, string>
            {
                ["title"] = deviceInfo.Title,
                ["hardware"] = deviceInfo.Hardware,
                ["software"] = deviceInfo.Software
            };

            await PostAsync<ApiResponse>("/v1/device/notify", formData, null, cancellationToken);
        }

        public Task<DevicesResponse> DevicesAsync(CancellationToken cancellationToken = default)
        {
            return GetAsync<DevicesResponse>("/v1/device", null, cancellationToken);
        }

        public Task<DeviceRemoveResponse> DeviceRemoveByIdAsync(string deviceId, CancellationToken cancellationToken = default)
        {
            return PostAsync<DeviceRemoveResponse>($"/v1/device/{deviceId}/remove", null, null, cancellationToken);
        }

        // Content Methods

        public Task<TypesResponse> GetTypesAsync(CancellationToken cancellationToken = default)
        {
            return _cache.GetOrSetAsync(
                "types",
                () => GetAsync<TypesResponse>("/v1/types", null, cancellationToken),
                TimeSpan.FromHours(1) // Types rarely change
            );
        }

        public Task<GenresResponse> GetGenresAsync(CancellationToken cancellationToken = default)
        {
            return _cache.GetOrSetAsync(
                "genres",
                () => GetAsync<GenresResponse>("/v1/genres", null, cancellationToken),
                TimeSpan.FromHours(1) // Genres rarely change
            );
        }

        public Task<CountriesResponse> GetCountriesAsync(CancellationToken cancellationToken = default)
        {
            return _cache.GetOrSetAsync(
                "countries",
                () => GetAsync<CountriesResponse>("/v1/countries", null, cancellationToken),
                TimeSpan.FromHours(1) // Countries rarely change
            );
        }

        public Task<ItemsResponse> GetItemsAsync(string? type = null, int page = 1, int perpage = 20, string? sort = null, CancellationToken cancellationToken = default)
        {
            var queryParams = new Dictionary<string, string>
            {
                ["page"] = page.ToString(),
                ["perpage"] = perpage.ToString()
            };

            if (!string.IsNullOrEmpty(type))
                queryParams["type"] = type;

            if (!string.IsNullOrEmpty(sort))
                queryParams["sort"] = sort;

            return GetAsync<ItemsResponse>("/v1/items", queryParams, cancellationToken);
        }

        public Task<ItemsResponse> GetFreshItemsAsync(string type, int page = 1, int perpage = 20, CancellationToken cancellationToken = default)
        {
            var queryParams = new Dictionary<string, string>
            {
                ["type"] = type,
                ["page"] = page.ToString(),
                ["perpage"] = perpage.ToString()
            };

            return GetAsync<ItemsResponse>("/v1/items/fresh", queryParams, cancellationToken);
        }

        public Task<ItemsResponse> GetHotItemsAsync(string type, int page = 1, int perpage = 20, CancellationToken cancellationToken = default)
        {
            var queryParams = new Dictionary<string, string>
            {
                ["type"] = type,
                ["page"] = page.ToString(),
                ["perpage"] = perpage.ToString()
            };

            return GetAsync<ItemsResponse>("/v1/items/hot", queryParams, cancellationToken);
        }

        public Task<ItemsResponse> GetPopularItemsAsync(string type, int page = 1, int perpage = 20, CancellationToken cancellationToken = default)
        {
            var queryParams = new Dictionary<string, string>
            {
                ["type"] = type,
                ["page"] = page.ToString(),
                ["perpage"] = perpage.ToString()
            };

            return GetAsync<ItemsResponse>("/v1/items/popular", queryParams, cancellationToken);
        }

        public Task<SearchResponse> SearchItemsAsync(string query, string? type = null, int page = 1, int perpage = 20, CancellationToken cancellationToken = default)
        {
            var queryParams = new Dictionary<string, string>
            {
                ["q"] = query,
                ["page"] = page.ToString(),
                ["perpage"] = perpage.ToString()
            };

            if (!string.IsNullOrEmpty(type))
                queryParams["type"] = type;

            return GetAsync<SearchResponse>("/v1/items/search", queryParams, cancellationToken);
        }

        public Task<ItemsResponse> GetSimilarItemsAsync(string itemId, CancellationToken cancellationToken = default)
        {
            var queryParams = new Dictionary<string, string>
            {
                ["id"] = itemId
            };

            return GetAsync<ItemsResponse>("/v1/items/similar", queryParams, cancellationToken);
        }

        public Task<ItemsResponse> GetItemsWithFiltersAsync(string? type = null, string? genre = null, string? country = null, string? year = null, int page = 1, int perpage = 20, CancellationToken cancellationToken = default)
        {
            var queryParams = new Dictionary<string, string>
            {
                ["page"] = page.ToString(),
                ["perpage"] = perpage.ToString()
            };

            if (!string.IsNullOrEmpty(type))
                queryParams["type"] = type;

            if (!string.IsNullOrEmpty(genre))
                queryParams["genre"] = genre;

            if (!string.IsNullOrEmpty(country))
                queryParams["country"] = country;

            if (!string.IsNullOrEmpty(year))
                queryParams["year"] = year;

            return GetAsync<ItemsResponse>("/v1/items", queryParams, cancellationToken);
        }

        public Task<ItemMediaResponse> GetItemMediaAsync(string id, CancellationToken cancellationToken = default)
        {
            var queryParams = new Dictionary<string, string>
            {
                ["nolinks"] = "0"
            };

            return GetAsync<ItemMediaResponse>($"/v1/items/{id}", queryParams, cancellationToken);
        }

        public Task<ItemMediaLinksResponse> GetItemMediaLinksAsync(string mediaId, CancellationToken cancellationToken = default)
        {
            var queryParams = new Dictionary<string, string>
            {
                ["mid"] = mediaId
            };

            return GetAsync<ItemMediaLinksResponse>("/v1/items/media-links", queryParams, cancellationToken);
        }

        public Task<BookmarksResponse> GetBookmarksAsync(CancellationToken cancellationToken = default)
        {
            return GetAsync<BookmarksResponse>("/v1/bookmarks", null, cancellationToken);
        }

        public Task<BookmarkItemsResponse> GetBookmarkItemsAsync(string bookmarkId, int page = 1, int perpage = 20, CancellationToken cancellationToken = default)
        {
            var queryParams = new Dictionary<string, string>
            {
                ["page"] = page.ToString(),
                ["perpage"] = perpage.ToString()
            };

            return GetAsync<BookmarkItemsResponse>($"/v1/bookmarks/{bookmarkId}", queryParams, cancellationToken);
        }

        public Task<CollectionsResponse> GetCollectionsAsync(int page = 1, int perpage = 20, CancellationToken cancellationToken = default)
        {
            var queryParams = new Dictionary<string, string>
            {
                ["page"] = page.ToString(),
                ["perpage"] = perpage.ToString(),
                ["sort"] = "updated-"
            };

            return GetAsync<CollectionsResponse>("/v1/collections", queryParams, cancellationToken);
        }

        public Task<CollectionItemsResponse> GetCollectionItemsAsync(string collectionId, CancellationToken cancellationToken = default)
        {
            var queryParams = new Dictionary<string, string>
            {
                ["id"] = collectionId
            };

            return GetAsync<CollectionItemsResponse>("/v1/collections/view", queryParams, cancellationToken);
        }

        public Task<HistoryResponse> GetHistoryAsync(int page = 1, int perpage = 20, CancellationToken cancellationToken = default)
        {
            var queryParams = new Dictionary<string, string>
            {
                ["page"] = page.ToString(),
                ["perpage"] = perpage.ToString()
            };

            return GetAsync<HistoryResponse>("/v1/history", queryParams, cancellationToken);
        }

        public Task<WatchingItemsResponse> GetWatchingSerialsAsync(CancellationToken cancellationToken = default)
        {
            var queryParams = new Dictionary<string, string>
            {
                ["subscribed"] = "0"
            };

            return GetAsync<WatchingItemsResponse>("/v1/watching/serials", queryParams, cancellationToken);
        }

        public Task<ChannelsResponse> GetChannelsAsync(CancellationToken cancellationToken = default)
        {
            return GetAsync<ChannelsResponse>("/v1/tv", null, cancellationToken);
        }

        public async Task MarkWatchTimeAsync(string itemId, int time, int video, int? season = null, CancellationToken cancellationToken = default)
        {
            var queryParams = new Dictionary<string, string>
            {
                ["id"] = itemId,
                ["time"] = time.ToString(),
                ["video"] = video.ToString()
            };

            if (season.HasValue)
                queryParams["season"] = season.Value.ToString();

            await GetAsync<ApiResponse>("/v1/watching/marktime", queryParams, cancellationToken);
        }

        public async Task<WatchingToggleResponse> ToggleWatchedAsync(string itemId, int? video = null, int? season = null, int? status = null, CancellationToken cancellationToken = default)
        {
            var queryParams = new Dictionary<string, string>
            {
                ["id"] = itemId
            };

            if (video.HasValue)
                queryParams["video"] = video.Value.ToString();

            if (season.HasValue)
                queryParams["season"] = season.Value.ToString();

            if (status.HasValue)
                queryParams["status"] = status.Value.ToString();

            return await GetAsync<WatchingToggleResponse>("/v1/watching/toggle", queryParams, cancellationToken);
        }

        public async Task<WatchingToggleWatchlistResponse> ToggleWatchlistAsync(string itemId, CancellationToken cancellationToken = default)
        {
            var queryParams = new Dictionary<string, string>
            {
                ["id"] = itemId
            };

            return await GetAsync<WatchingToggleWatchlistResponse>("/v1/watching/togglewatchlist", queryParams, cancellationToken);
        }
    }
}
