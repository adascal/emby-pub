using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.Movies;
using MediaBrowser.Controller.Entities.TV;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Model.Entities;

namespace Kinopub.Plugin.Providers
{
    public class KinopubMovieExternalId : IExternalId
    {
        public string Name => "Kinopub";

        public string Key => "Kinopub";

        public string UrlFormatString => "https://service-kp.com/item/{0}";

        public bool Supports(IHasProviderIds item)
        {
            return item is Movie;
        }
    }

    public class KinopubSeriesExternalId : IExternalId
    {
        public string Name => "Kinopub";

        public string Key => "Kinopub";

        public string UrlFormatString => "https://service-kp.com/item/{0}";

        public bool Supports(IHasProviderIds item)
        {
            return item is Series;
        }
    }
}
