using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediaBrowser.Controller.Channels;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Model.Channels;
using MediaBrowser.Model.Dto;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.Logging;
using MediaBrowser.Model.MediaInfo;
using Kinopub.Plugin.Api;
using Kinopub.Plugin.Models;

namespace Kinopub.Plugin.Channel
{
    public class KinopubChannel : IChannel, IHasCacheKey, ISupportsMediaProbe
    {
        private readonly ILogger _logger;

        public KinopubChannel(ILogManager logManager)
        {
            _logger = logManager.GetLogger(GetType().Name);
        }

        public string Name => "Kinopub";

        public string Description => "Stream movies and TV shows from Kinopub";

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

                    case "all":
                    case "fresh":
                    case "hot":
                    case "popular":
                        if (parts.Length > 1)
                        {
                            return await GetItemsByCategory(apiClient, category, parts[1], query, cancellationToken);
                        }
                        break;

                    case "filters":
                        if (parts.Length > 1)
                        {
                            return await GetFilters(apiClient, parts[1], cancellationToken);
                        }
                        break;

                    case "genreslist":
                        if (parts.Length > 1)
                        {
                            return await GetGenresList(apiClient, parts[1], cancellationToken);
                        }
                        break;

                    case "countrieslist":
                        if (parts.Length > 1)
                        {
                            return await GetCountriesList(apiClient, parts[1], cancellationToken);
                        }
                        break;

                    case "yearslist":
                        if (parts.Length > 1)
                        {
                            return await GetYearsList(apiClient, parts[1], cancellationToken);
                        }
                        break;

                    case "genre":
                        if (parts.Length > 2)
                        {
                            return await GetItemsByGenre(apiClient, parts[1], parts[2], query, cancellationToken);
                        }
                        break;

                    case "country":
                        if (parts.Length > 2)
                        {
                            return await GetItemsByCountry(apiClient, parts[1], parts[2], query, cancellationToken);
                        }
                        break;

                    case "year":
                        if (parts.Length > 2)
                        {
                            return await GetItemsByYear(apiClient, parts[1], parts[2], query, cancellationToken);
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

                    case "collections":
                        return await GetCollections(apiClient, query, cancellationToken);

                    case "collection":
                        if (parts.Length > 1)
                        {
                            return await GetCollectionItems(apiClient, parts[1], cancellationToken);
                        }
                        break;

                    case "history":
                        return await GetHistory(apiClient, query, cancellationToken);

                    case "watching":
                        return await GetWatchingSerials(apiClient, cancellationToken);

                    case "channels":
                        return await GetLiveTVChannels(apiClient, cancellationToken);

                    case "item":
                        if (parts.Length > 1)
                        {
                            return await GetItemDetails(apiClient, parts[1], cancellationToken);
                        }
                        break;

                    case "season":
                        if (parts.Length > 2)
                        {
                            return await GetSeasonEpisodes(apiClient, parts[1], parts[2], cancellationToken);
                        }
                        break;

                    case "similar":
                        if (parts.Length > 1)
                        {
                            return await GetSimilarItems(apiClient, parts[1], cancellationToken);
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
                    Id = "watching",
                    Name = "Continue Watching",
                    Type = ChannelItemType.Folder,
                    ImageUrl = null
                },
                new ChannelItemInfo
                {
                    Id = "collections",
                    Name = "Collections",
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
                    Id = "channels",
                    Name = "Live TV",
                    Type = ChannelItemType.Folder,
                    ImageUrl = null
                },
                new ChannelItemInfo
                {
                    Id = "history",
                    Name = "Watch History",
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

        private async Task<ChannelItemResult> GetContentTypes(KinopubApiClient apiClient, CancellationToken cancellationToken)
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
                    Id = $"all_{typeId}",
                    Name = "All",
                    Type = ChannelItemType.Folder,
                    ImageUrl = null
                },
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
                },
                new ChannelItemInfo
                {
                    Id = $"filters_{typeId}",
                    Name = "Filters (Genres/Countries)",
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

        private async Task<ChannelItemResult> GetItemsByCategory(KinopubApiClient apiClient, string category, string typeId, InternalChannelItemQuery query, CancellationToken cancellationToken)
        {
            var page = (query.StartIndex ?? 0) / (query.Limit ?? 20) + 1;
            var perPage = query.Limit ?? 20;

            // Determine sort order based on query or use defaults
            string? sort = null;
            if (query.SortBy != null)
            {
                var sortField = query.SortBy switch
                {
                    ChannelItemSortField.Name => "title",
                    ChannelItemSortField.DateCreated => "created",
                    ChannelItemSortField.CommunityRating => "rating",
                    _ => "updated"
                };
                sort = query.SortDescending ? $"{sortField}-" : sortField;
            }

            ItemsResponse response;

            switch (category)
            {
                case "all":
                    response = await apiClient.GetItemsAsync(typeId, page, perPage, sort, cancellationToken);
                    break;
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

        private async Task<ChannelItemResult> GetBookmarks(KinopubApiClient apiClient, CancellationToken cancellationToken)
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

        private async Task<ChannelItemResult> GetBookmarkItems(KinopubApiClient apiClient, string bookmarkId, InternalChannelItemQuery query, CancellationToken cancellationToken)
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


        private async Task<ChannelItemResult> GetItemDetails(KinopubApiClient apiClient, string itemId, CancellationToken cancellationToken)
        {
            var response = await apiClient.GetItemMediaAsync(itemId, cancellationToken);
            var item = response.Item;

            var items = new List<ChannelItemInfo>();

            // Add trailer if available
            if (item.Trailer != null && !string.IsNullOrEmpty(item.Trailer.Url))
            {
                items.Add(new ChannelItemInfo
                {
                    Id = $"trailer_{itemId}",
                    Name = "Trailer",
                    Type = ChannelItemType.Media,
                    ContentType = ChannelMediaContentType.Trailer,
                    MediaType = ChannelMediaType.Video,
                    ImageUrl = item.Posters?.Medium,
                    MediaSources = new List<MediaSourceInfo>
                    {
                        new MediaSourceInfo
                        {
                            Id = item.Trailer.Id,
                            Path = item.Trailer.Url,
                            Protocol = MediaProtocol.Http,
                            Container = item.Trailer.Url.Contains(".m3u8") ? "hls" : "mp4",
                            SupportsDirectStream = true,
                            SupportsDirectPlay = true
                        }
                    }
                });
            }

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

            // Add "Similar Items" folder if not a trailer
            items.Add(new ChannelItemInfo
            {
                Id = $"similar_{itemId}",
                Name = "Similar Items",
                Type = ChannelItemType.Folder,
                ImageUrl = null
            });

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

        private ChannelItemInfo ConvertVideoToChannelItem(Kinopub.Plugin.Models.Video video, ItemDetails parentItem)
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
                        var videoStream = new MediaStream
                        {
                            Type = MediaStreamType.Video,
                            Width = file.W,
                            Height = file.H,
                            Codec = file.Codec,
                            IsInterlaced = false
                        };

                        var source = new MediaSourceInfo
                        {
                            Id = video.Id,
                            Path = preferredUrl,
                            Protocol = MediaProtocol.Http,
                            Container = preferredUrl.Contains(".m3u8") ? "hls" : "mp4",
                            SupportsDirectStream = true,
                            SupportsDirectPlay = true
                        };

                        // Add audio streams
                        if (video.Audios != null && video.Audios.Count > 0)
                        {
                            source.MediaStreams = new List<MediaStream>();
                            source.MediaStreams.Add(videoStream);

                            for (int i = 0; i < video.Audios.Count; i++)
                            {
                                var audio = video.Audios[i];
                                var audioStream = new MediaStream
                                {
                                    Type = MediaStreamType.Audio,
                                    Index = i + 1,
                                    Codec = audio.Codec,
                                    Language = audio.Lang,
                                    Channels = audio.Channels,
                                    Title = audio.Type?.Title ?? audio.Lang
                                };
                                source.MediaStreams.Add(audioStream);
                            }

                            // Add subtitle streams
                            if (video.Subtitles != null && video.Subtitles.Count > 0)
                            {
                                int subtitleIndex = video.Audios.Count + 1;
                                foreach (var subtitle in video.Subtitles)
                                {
                                    var subStream = new MediaStream
                                    {
                                        Type = MediaStreamType.Subtitle,
                                        Index = subtitleIndex++,
                                        Language = subtitle.Lang,
                                        IsExternal = !subtitle.Embed,
                                        IsForced = subtitle.Forced,
                                        Path = subtitle.Url,
                                        Title = subtitle.Lang
                                    };
                                    source.MediaStreams.Add(subStream);
                                }
                            }
                        }

                        // Set resume position if available
                        if (video.Watching?.Time > 0)
                        {
                            source.RunTimeTicks = TimeSpan.FromSeconds(video.Duration).Ticks;
                        }

                        mediaSource.Add(source);
                    }
                }
            }

            var channelItem = new ChannelItemInfo
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

            // Set date added from parent
            if (parentItem.CreatedAt > 0)
            {
                channelItem.DateCreated = DateTimeOffset.FromUnixTimeSeconds(parentItem.CreatedAt).DateTime;
            }

            return channelItem;
        }

        private async Task<ChannelItemResult> GetSeasonEpisodes(KinopubApiClient apiClient, string itemId, string seasonId, CancellationToken cancellationToken)
        {
            var response = await apiClient.GetItemMediaAsync(itemId, cancellationToken);
            var item = response.Item;

            var items = new List<ChannelItemInfo>();

            if (item.Seasons != null)
            {
                var season = item.Seasons.FirstOrDefault(s => s.Id == seasonId);
                if (season?.Episodes != null)
                {
                    foreach (var episode in season.Episodes)
                    {
                        var channelItem = ConvertVideoToChannelItem(episode, item);
                        channelItem.ContentType = ChannelMediaContentType.Episode;
                        channelItem.IndexNumber = episode.Number;
                        channelItem.ParentIndexNumber = season.Number;
                        items.Add(channelItem);
                    }
                }
            }

            return new ChannelItemResult
            {
                Items = items,
                TotalRecordCount = items.Count
            };
        }

        private async Task<ChannelItemResult> GetCollections(KinopubApiClient apiClient, InternalChannelItemQuery query, CancellationToken cancellationToken)
        {
            var page = (query.StartIndex ?? 0) / (query.Limit ?? 20) + 1;
            var perPage = query.Limit ?? 20;

            var response = await apiClient.GetCollectionsAsync(page, perPage, cancellationToken);

            var items = response.Items.Select(collection => new ChannelItemInfo
            {
                Id = $"collection_{collection.Id}",
                Name = collection.Title,
                Type = ChannelItemType.Folder,
                ImageUrl = collection.Posters?.Medium,
                Overview = $"Views: {collection.Views}, Watchers: {collection.Watchers}"
            }).ToList();

            return new ChannelItemResult
            {
                Items = items,
                TotalRecordCount = response.Pagination?.TotalItems ?? items.Count
            };
        }

        private async Task<ChannelItemResult> GetCollectionItems(KinopubApiClient apiClient, string collectionId, CancellationToken cancellationToken)
        {
            var response = await apiClient.GetCollectionItemsAsync(collectionId, cancellationToken);

            var items = response.Items.Select(ConvertToChannelItem).ToList();

            return new ChannelItemResult
            {
                Items = items,
                TotalRecordCount = items.Count
            };
        }

        private async Task<ChannelItemResult> GetHistory(KinopubApiClient apiClient, InternalChannelItemQuery query, CancellationToken cancellationToken)
        {
            var page = (query.StartIndex ?? 0) / (query.Limit ?? 20) + 1;
            var perPage = query.Limit ?? 20;

            var response = await apiClient.GetHistoryAsync(page, perPage, cancellationToken);

            var items = response.History.Select(historyItem =>
            {
                var channelItem = ConvertToChannelItem(historyItem.Item);
                channelItem.Name = $"{historyItem.Item.Title} - {historyItem.Media.Title}";
                return channelItem;
            }).ToList();

            return new ChannelItemResult
            {
                Items = items,
                TotalRecordCount = response.Pagination?.TotalItems ?? items.Count
            };
        }

        private async Task<ChannelItemResult> GetWatchingSerials(KinopubApiClient apiClient, CancellationToken cancellationToken)
        {
            var response = await apiClient.GetWatchingSerialsAsync(cancellationToken);

            var items = response.Items.Select(item =>
            {
                var channelItem = ConvertToChannelItem(item);
                if (item.New > 0)
                {
                    channelItem.Name = $"{item.Title} ({item.New} new)";
                }
                return channelItem;
            }).ToList();

            return new ChannelItemResult
            {
                Items = items,
                TotalRecordCount = items.Count
            };
        }

        private async Task<ChannelItemResult> GetLiveTVChannels(KinopubApiClient apiClient, CancellationToken cancellationToken)
        {
            var response = await apiClient.GetChannelsAsync(cancellationToken);

            var items = response.Channels.Select(channel => new ChannelItemInfo
            {
                Id = $"channel_{channel.Id}",
                Name = channel.Title,
                Type = ChannelItemType.Media,
                ContentType = ChannelMediaContentType.Movie,
                MediaType = ChannelMediaType.Video,
                ImageUrl = channel.Logos?.M,
                MediaSources = new List<MediaSourceInfo>
                {
                    new MediaSourceInfo
                    {
                        Id = channel.Id,
                        Path = channel.Stream,
                        Protocol = MediaProtocol.Http,
                        Container = channel.Stream.Contains(".m3u8") ? "hls" : "mp4",
                        SupportsDirectStream = true,
                        SupportsDirectPlay = true,
                        IsInfiniteStream = true
                    }
                }
            }).ToList();

            return new ChannelItemResult
            {
                Items = items,
                TotalRecordCount = items.Count
            };
        }

        private async Task<ChannelItemResult> GetSimilarItems(KinopubApiClient apiClient, string itemId, CancellationToken cancellationToken)
        {
            var response = await apiClient.GetSimilarItemsAsync(itemId, cancellationToken);

            var items = response.Items.Select(ConvertToChannelItem).ToList();

            return new ChannelItemResult
            {
                Items = items,
                TotalRecordCount = items.Count
            };
        }

        private Task<ChannelItemResult> GetFilters(KinopubApiClient apiClient, string typeId, CancellationToken cancellationToken)
        {
            var items = new List<ChannelItemInfo>
            {
                new ChannelItemInfo
                {
                    Id = $"genreslist_{typeId}",
                    Name = "Browse by Genre",
                    Type = ChannelItemType.Folder,
                    ImageUrl = null
                },
                new ChannelItemInfo
                {
                    Id = $"countrieslist_{typeId}",
                    Name = "Browse by Country",
                    Type = ChannelItemType.Folder,
                    ImageUrl = null
                },
                new ChannelItemInfo
                {
                    Id = $"yearslist_{typeId}",
                    Name = "Browse by Year",
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

        private async Task<ChannelItemResult> GetGenresList(KinopubApiClient apiClient, string typeId, CancellationToken cancellationToken)
        {
            var response = await apiClient.GetGenresAsync(cancellationToken);

            // Map type to genre type (movie, music, docu, tvshow)
            var genreType = typeId switch
            {
                "1" or "2" or "3" => "movie", // movie, serial, 3D
                "4" => "music", // concert
                "5" or "6" => "docu", // documovie, docuserial
                "7" => "tvshow", // tvshow
                _ => "movie"
            };

            var items = response.Items
                .Where(g => g.Type == genreType)
                .Select(genre => new ChannelItemInfo
                {
                    Id = $"genre_{typeId}_{genre.Id}",
                    Name = genre.Title,
                    Type = ChannelItemType.Folder,
                    ImageUrl = null
                }).ToList();

            return new ChannelItemResult
            {
                Items = items,
                TotalRecordCount = items.Count
            };
        }

        private async Task<ChannelItemResult> GetCountriesList(KinopubApiClient apiClient, string typeId, CancellationToken cancellationToken)
        {
            var response = await apiClient.GetCountriesAsync(cancellationToken);

            var items = response.Items.Select(country => new ChannelItemInfo
            {
                Id = $"country_{typeId}_{country.Id}",
                Name = country.Title,
                Type = ChannelItemType.Folder,
                ImageUrl = null
            }).ToList();

            return new ChannelItemResult
            {
                Items = items,
                TotalRecordCount = items.Count
            };
        }

        private Task<ChannelItemResult> GetYearsList(KinopubApiClient apiClient, string typeId, CancellationToken cancellationToken)
        {
            // Generate a list of years from current year back to 1960
            var currentYear = DateTime.Now.Year;
            var years = Enumerable.Range(1960, currentYear - 1960 + 1).Reverse();

            var items = years.Select(year => new ChannelItemInfo
            {
                Id = $"year_{typeId}_{year}",
                Name = year.ToString(),
                Type = ChannelItemType.Folder,
                ImageUrl = null
            }).ToList();

            return Task.FromResult(new ChannelItemResult
            {
                Items = items,
                TotalRecordCount = items.Count
            });
        }

        private async Task<ChannelItemResult> GetItemsByGenre(KinopubApiClient apiClient, string typeId, string genreId, InternalChannelItemQuery query, CancellationToken cancellationToken)
        {
            var page = (query.StartIndex ?? 0) / (query.Limit ?? 20) + 1;
            var perPage = query.Limit ?? 20;

            var response = await apiClient.GetItemsWithFiltersAsync(typeId, genreId, null, null, page, perPage, cancellationToken);

            var items = response.Items.Select(ConvertToChannelItem).ToList();

            return new ChannelItemResult
            {
                Items = items,
                TotalRecordCount = response.Pagination?.TotalItems ?? items.Count
            };
        }

        private async Task<ChannelItemResult> GetItemsByCountry(KinopubApiClient apiClient, string typeId, string countryId, InternalChannelItemQuery query, CancellationToken cancellationToken)
        {
            var page = (query.StartIndex ?? 0) / (query.Limit ?? 20) + 1;
            var perPage = query.Limit ?? 20;

            var response = await apiClient.GetItemsWithFiltersAsync(typeId, null, countryId, null, page, perPage, cancellationToken);

            var items = response.Items.Select(ConvertToChannelItem).ToList();

            return new ChannelItemResult
            {
                Items = items,
                TotalRecordCount = response.Pagination?.TotalItems ?? items.Count
            };
        }

        private async Task<ChannelItemResult> GetItemsByYear(KinopubApiClient apiClient, string typeId, string year, InternalChannelItemQuery query, CancellationToken cancellationToken)
        {
            var page = (query.StartIndex ?? 0) / (query.Limit ?? 20) + 1;
            var perPage = query.Limit ?? 20;

            var response = await apiClient.GetItemsWithFiltersAsync(typeId, null, null, year, page, perPage, cancellationToken);

            var items = response.Items.Select(ConvertToChannelItem).ToList();

            return new ChannelItemResult
            {
                Items = items,
                TotalRecordCount = response.Pagination?.TotalItems ?? items.Count
            };
        }

        public Task<DynamicImageResponse> GetChannelImage(ImageType type, CancellationToken cancellationToken)
        {
            return Task.FromResult(new DynamicImageResponse());
        }

        public IEnumerable<ImageType> GetSupportedChannelImages()
        {
            return new List<ImageType>
            {
                ImageType.Primary,
                ImageType.Thumb
            };
        }
    }
}
