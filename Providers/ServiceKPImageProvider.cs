using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediaBrowser.Common.Net;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.Movies;
using MediaBrowser.Controller.Entities.TV;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Model.Configuration;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.Logging;
using MediaBrowser.Model.Providers;

namespace ServiceKP.Plugin.Providers
{
    public class ServiceKPImageProvider : IRemoteImageProvider
    {
        private readonly IHttpClient _httpClient;
        private readonly ILogger _logger;

        public string Name => "ServiceKP";

        public ServiceKPImageProvider(IHttpClient httpClient, ILogManager logManager)
        {
            _httpClient = httpClient;
            _logger = logManager.GetLogger(GetType().Name);
        }

        public bool Supports(BaseItem item)
        {
            return item is Movie || item is Series;
        }

        public IEnumerable<ImageType> GetSupportedImages(BaseItem item)
        {
            return new List<ImageType>
            {
                ImageType.Primary,
                ImageType.Backdrop,
                ImageType.Thumb
            };
        }

        public async Task<IEnumerable<RemoteImageInfo>> GetImages(BaseItem item, LibraryOptions libraryOptions, CancellationToken cancellationToken)
        {
            var apiClient = Plugin.Instance?.GetApiClient();
            if (apiClient == null)
            {
                return Enumerable.Empty<RemoteImageInfo>();
            }

            var serviceKpId = item.GetProviderId("ServiceKP");
            if (string.IsNullOrEmpty(serviceKpId))
            {
                return Enumerable.Empty<RemoteImageInfo>();
            }

            try
            {
                var itemResponse = await apiClient.GetItemMediaAsync(serviceKpId, cancellationToken);
                var apiItem = itemResponse?.Item;

                if (apiItem?.Posters == null)
                {
                    return Enumerable.Empty<RemoteImageInfo>();
                }

                var images = new List<RemoteImageInfo>();

                // Add poster images
                if (!string.IsNullOrEmpty(apiItem.Posters.Big))
                {
                    images.Add(new RemoteImageInfo
                    {
                        Url = apiItem.Posters.Big,
                        Type = ImageType.Primary,
                        ProviderName = Name
                    });
                }

                if (!string.IsNullOrEmpty(apiItem.Posters.Medium))
                {
                    images.Add(new RemoteImageInfo
                    {
                        Url = apiItem.Posters.Medium,
                        Type = ImageType.Thumb,
                        ProviderName = Name
                    });
                }

                // Add wide poster as backdrop if available
                if (!string.IsNullOrEmpty(apiItem.Posters.Wide))
                {
                    images.Add(new RemoteImageInfo
                    {
                        Url = apiItem.Posters.Wide,
                        Type = ImageType.Backdrop,
                        ProviderName = Name
                    });
                }

                return images;
            }
            catch (Exception ex)
            {
                _logger.Error($"Error fetching images for item {serviceKpId}: {ex.Message}");
                return Enumerable.Empty<RemoteImageInfo>();
            }
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
