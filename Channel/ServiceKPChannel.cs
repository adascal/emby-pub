using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediaBrowser.Controller.Channels;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Model.Channels;
using MediaBrowser.Model.Dto;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.Logging;
using MediaBrowser.Model.MediaInfo;
using ServiceKP.Plugin.Api;
using ServiceKP.Plugin.Models;

namespace ServiceKP.Plugin.Channel
{
    public class ServiceKPChannel : IChannel, IHasCacheKey, ISupportsLatestMedia, ISupportsMediaProbe
    {
        private readonly ILogger _logger;

        public ServiceKPChannel(ILogManager logManager)
        {
            _logger = logManager.GetLogger(GetType().Name);
        }

        public string Name => "ServiceKP";

        public string Description => "Stream movies and TV shows from ServiceKP";

        public string DataVersion => "1";

        public string HomePageUrl => "https://service-kp.com";

        public ChannelParentalRating ParentalRating => ChannelParentalRating.GeneralAudience;

        public InternalChannelFeatures GetChannelFeatures()
        {
            return new InternalChannelFeatures
            {
                ContentTypes = new List<ChannelMediaContentType>
                {
                    ChannelMediaContentType.Movie,
                    ChannelMediaContentType.Episode
                },
                MediaTypes = new List<ChannelMediaType>
                {
                    ChannelMediaType.Video
                },
                SupportsContentDownloading = false,
                SupportsSortOrderToggle = true,
                DefaultSortFields = new List<ChannelItemSortField>
                {
                    ChannelItemSortField.DateCreated,
                    ChannelItemSortField.Name,
                    ChannelItemSortField.CommunityRating
                },
                MaxPageSize = 50
            };
        }

        public bool IsEnabledFor(string userId)
        {
            return true;
        }

        public string GetCacheKey(string userId)
        {
            return $"servicekp_{DataVersion}";
        }

        public async Task<ChannelItemResult> GetChannelItems(InternalChannelItemQuery query, CancellationToken cancellationToken)
        {
            _logger.Info($"GetChannelItems: FolderId={query.FolderId}, StartIndex={query.StartIndex}, Limit={query.Limit}");

            var apiClient = Plugin.Instance?.GetApiClient();
            if (apiClient == null)
            {
                return new ChannelItemResult { Items = new List<ChannelItemInfo>() };
            }

            try
            {
                // Root folder - show main categories
                if (string.IsNullOrEmpty(query.FolderId))
                {
                    return GetRootCategories();
                }

                // Parse folder ID to determine what to show
                var parts = query.FolderId.Split('_');
                var category = parts[0];

                switch (category)
                {
                    case "types":
                        return await GetContentTypes(apiClient, cancellationToken);

                    case "type":
                        if (parts.Length > 1)
                        {
                            return await GetTypeSubcategories(parts[1]);
                        }
                        break;

                    case "fresh":
                    case "hot":
                    case "popular":
                        if (parts.Length > 1)
                        {
                            return await GetItemsByCategory(apiClient, category, parts[1], query, cancellationToken);
                        }
                        break;

                    case "bookmarks":
                        return await GetBookmarks(apiClient, cancellationToken);

                    case "bookmark":
                        if (parts.Length > 1)
                        {
                            return await GetBookmarkItems(apiClient, parts[1], query, cancellationToken);
                        }
                        break;

                    case "search":
                        return await SearchItems(apiClient, query, cancellationToken);

                    case "item":
                        if (parts.Length > 1)
                        {
                            return await GetItemDetails(apiClient, parts[1], cancellationToken);
                        }
                        break;
                }

                return new ChannelItemResult { Items = new List<ChannelItemInfo>() };
            }
            catch (Exception ex)
            {
                _logger.Error($"Error getting channel items: {ex.Message}");
                return new ChannelItemResult { Items = new List<ChannelItemInfo>() };
            }
        }

        private ChannelItemResult GetRootCategories()
        {
            var items = new List<ChannelItemInfo>
            {
                new ChannelItemInfo
                {
                    Id = "types",
                    Name = "Browse by Type",
                    Type = ChannelItemType.Folder,
                    ImageUrl = null
                },
                new ChannelItemInfo
                {
                    Id = "bookmarks",
                    Name = "My Bookmarks",
                    Type = ChannelItemType.Folder,
                    ImageUrl = null
                },
                new ChannelItemInfo
                {
                    Id = "search",
                    Name = "Search",
                    Type = ChannelItemType.Folder,
                    ImageUrl = null
                }
            };

            return new ChannelItemResult
            {
                Items = items,
                TotalRecordCount = items.Count
            };
        }

        private async Task<ChannelItemResult> GetContentTypes(ServiceKPApiClient apiClient, CancellationToken cancellationToken)
        {
            var typesResponse = await apiClient.GetTypesAsync(cancellationToken);

            var items = typesResponse.Items.Select(type => new ChannelItemInfo
            {
                Id = $"type_{type.Id}",
                Name = type.Title,
                Type = ChannelItemType.Folder,
                ImageUrl = null
            }).ToList();

            return new ChannelItemResult
            {
                Items = items,
                TotalRecordCount = items.Count
            };
        }

        private Task<ChannelItemResult> GetTypeSubcategories(string typeId)
        {
            var items = new List<ChannelItemInfo>
            {
                new ChannelItemInfo
                {
                    Id = $"fresh_{typeId}",
                    Name = "Fresh",
                    Type = ChannelItemType.Folder,
                    ImageUrl = null
                },
                new ChannelItemInfo
                {
                    Id = $"hot_{typeId}",
                    Name = "Hot",
                    Type = ChannelItemType.Folder,
                    ImageUrl = null
                },
                new ChannelItemInfo
                {
                    Id = $"popular_{typeId}",
                    Name = "Popular",
                    Type = ChannelItemType.Folder,
                    ImageUrl = null
                }
            };

            return Task.FromResult(new ChannelItemResult
            {
                Items = items,
                TotalRecordCount = items.Count
            });
        }

        private async Task<ChannelItemResult> GetItemsByCategory(ServiceKPApiClient apiClient, string category, string typeId, InternalChannelItemQuery query, CancellationToken cancellationToken)
        {
            var page = (query.StartIndex ?? 0) / (query.Limit ?? 20) + 1;
            var perPage = query.Limit ?? 20;

            ItemsResponse response;

            switch (category)
            {
                case "fresh":
                    response = await apiClient.GetFreshItemsAsync(typeId, page, perPage, cancellationToken);
                    break;
                case "hot":
                    response = await apiClient.GetHotItemsAsync(typeId, page, perPage, cancellationToken);
                    break;
                case "popular":
                    response = await apiClient.GetPopularItemsAsync(typeId, page, perPage, cancellationToken);
                    break;
                default:
                    response = new ItemsResponse();
                    break;
            }

            var items = response.Items.Select(ConvertToChannelItem).ToList();

            return new ChannelItemResult
            {
                Items = items,
                TotalRecordCount = response.Pagination?.TotalItems ?? items.Count
            };
        }

        private async Task<ChannelItemResult> GetBookmarks(ServiceKPApiClient apiClient, CancellationToken cancellationToken)
        {
            var response = await apiClient.GetBookmarksAsync(cancellationToken);

            var items = response.Items.Select(bookmark => new ChannelItemInfo
            {
                Id = $"bookmark_{bookmark.Id}",
                Name = $"{bookmark.Title} ({bookmark.Count})",
                Type = ChannelItemType.Folder,
                ImageUrl = null
            }).ToList();

            return new ChannelItemResult
            {
                Items = items,
                TotalRecordCount = items.Count
            };
        }

        private async Task<ChannelItemResult> GetBookmarkItems(ServiceKPApiClient apiClient, string bookmarkId, InternalChannelItemQuery query, CancellationToken cancellationToken)
        {
            var page = (query.StartIndex ?? 0) / (query.Limit ?? 20) + 1;
            var perPage = query.Limit ?? 20;

            var response = await apiClient.GetBookmarkItemsAsync(bookmarkId, page, perPage, cancellationToken);

            var items = response.Items.Select(ConvertToChannelItem).ToList();

            return new ChannelItemResult
            {
                Items = items,
                TotalRecordCount = response.Pagination?.TotalItems ?? items.Count
            };
        }

        private async Task<ChannelItemResult> SearchItems(ServiceKPApiClient apiClient, InternalChannelItemQuery query, CancellationToken cancellationToken)
        {
            if (string.IsNullOrEmpty(query.SearchTerm))
            {
                return new ChannelItemResult { Items = new List<ChannelItemInfo>() };
            }

            var page = (query.StartIndex ?? 0) / (query.Limit ?? 20) + 1;
            var perPage = query.Limit ?? 20;

            var response = await apiClient.SearchItemsAsync(query.SearchTerm, null, page, perPage, cancellationToken);

            var items = response.Items.Select(ConvertToChannelItem).ToList();

            return new ChannelItemResult
            {
                Items = items,
                TotalRecordCount = response.Pagination?.TotalItems ?? items.Count
            };
        }

        private async Task<ChannelItemResult> GetItemDetails(ServiceKPApiClient apiClient, string itemId, CancellationToken cancellationToken)
        {
            var response = await apiClient.GetItemMediaAsync(itemId, cancellationToken);
            var item = response.Item;

            var items = new List<ChannelItemInfo>();

            // For series, add seasons as folders
            if (item.Seasons != null && item.Seasons.Count > 0)
            {
                foreach (var season in item.Seasons)
                {
                    items.Add(new ChannelItemInfo
                    {
                        Id = $"season_{itemId}_{season.Id}",
                        Name = season.Title,
                        Type = ChannelItemType.Folder,
                        ImageUrl = item.Posters?.Medium,
                        FolderType = ChannelFolderType.Season
                    });
                }
            }
            // For movies or single videos
            else if (item.Videos != null && item.Videos.Count > 0)
            {
                foreach (var video in item.Videos)
                {
                    items.Add(ConvertVideoToChannelItem(video, item));
                }
            }

            return new ChannelItemResult
            {
                Items = items,
                TotalRecordCount = items.Count
            };
        }

        private ChannelItemInfo ConvertToChannelItem(Item item)
        {
            var channelItem = new ChannelItemInfo
            {
                Id = $"item_{item.Id}",
                Name = item.Title,
                Type = item.Type == "serial" ? ChannelItemType.Folder : ChannelItemType.Media,
                ContentType = item.Type == "serial" ? ChannelMediaContentType.Episode : ChannelMediaContentType.Movie,
                MediaType = ChannelMediaType.Video,
                ImageUrl = item.Posters?.Big,
                Overview = item.Plot,
                CommunityRating = (float)(item.ImdbRating > 0 ? item.ImdbRating : item.KinopoiskRating),
                PremiereDate = new DateTime(item.Year, 1, 1),
                ProductionYear = item.Year,
                Genres = item.Genres?.Select(g => g.Title).ToList() ?? new List<string>(),
                OfficialRating = item.Type == "serial" ? "TV-MA" : "R"
            };

            // Add people (cast/director)
            if (!string.IsNullOrEmpty(item.Cast))
            {
                channelItem.People = item.Cast.Split(',')
                    .Select(name => new PersonInfo { Name = name.Trim(), Type = PersonType.Actor })
                    .ToList();
            }

            return channelItem;
        }

        private ChannelItemInfo ConvertVideoToChannelItem(Video video, ItemDetails parentItem)
        {
            var mediaSource = new List<MediaSourceInfo>();

            if (video.Files != null && video.Files.Count > 0)
            {
                var file = video.Files.OrderByDescending(f => f.H).FirstOrDefault();
                if (file != null)
                {
                    var preferredUrl = Plugin.Instance?.Configuration.PreferredStreamingType switch
                    {
                        "hls" => file.Url.Hls,
                        "hls2" => file.Url.Hls2,
                        "hls4" => file.Url.Hls4,
                        _ => file.Url.Http
                    };

                    if (!string.IsNullOrEmpty(preferredUrl))
                    {
                        mediaSource.Add(new MediaSourceInfo
                        {
                            Id = video.Id,
                            Path = preferredUrl,
                            Protocol = MediaProtocol.Http,
                            Container = preferredUrl.Contains(".m3u8") ? "hls" : "mp4",
                            VideoType = VideoType.VideoFile,
                            SupportsDirectStream = true,
                            SupportsDirectPlay = true
                        });
                    }
                }
            }

            return new ChannelItemInfo
            {
                Id = $"video_{video.Id}",
                Name = video.Title,
                Type = ChannelItemType.Media,
                ContentType = ChannelMediaContentType.Movie,
                MediaType = ChannelMediaType.Video,
                ImageUrl = !string.IsNullOrEmpty(video.Thumbnail) ? video.Thumbnail : parentItem.Posters?.Medium,
                RunTimeTicks = TimeSpan.FromSeconds(video.Duration).Ticks,
                MediaSources = mediaSource
            };
        }

        public Task<DynamicImageResponse> GetChannelImage(ImageType type, CancellationToken cancellationToken)
        {
            return Task.FromResult(new DynamicImageResponse
            {
                HasImage = false
            });
        }

        public IEnumerable<ImageType> GetSupportedChannelImages()
        {
            return new List<ImageType>
            {
                ImageType.Primary,
                ImageType.Thumb
            };
        }

        public Task<IEnumerable<ChannelItemInfo>> GetLatestMedia(ChannelLatestMediaSearch request, CancellationToken cancellationToken)
        {
            return Task.FromResult<IEnumerable<ChannelItemInfo>>(new List<ChannelItemInfo>());
        }
    }
}
