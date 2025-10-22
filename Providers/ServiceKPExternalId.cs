using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.Movies;
using MediaBrowser.Controller.Entities.TV;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Model.Entities;

namespace ServiceKP.Plugin.Providers
{
    public class ServiceKPMovieExternalId : IExternalId
    {
        public string Name => "ServiceKP";

        public string Key => "ServiceKP";

        public string UrlFormatString => "https://service-kp.com/item/{0}";

        public bool Supports(IHasProviderIds item)
        {
            return item is Movie;
        }
    }

    public class ServiceKPSeriesExternalId : IExternalId
    {
        public string Name => "ServiceKP";

        public string Key => "ServiceKP";

        public string UrlFormatString => "https://service-kp.com/item/{0}";

        public bool Supports(IHasProviderIds item)
        {
            return item is Series;
        }
    }
}
