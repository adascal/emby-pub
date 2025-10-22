using System;
using MediaBrowser.Model.Plugins;

namespace ServiceKP.Plugin.Configuration
{
    public class PluginConfiguration : BasePluginConfiguration
    {
        public string ApiBaseUrl { get; set; } = "https://api.service-kp.com";
        public string ClientId { get; set; } = "xbmc";
        public string ClientSecret { get; set; } = "cgg3gtifu46urtfp2zp1nqtba0k2ezxh";

        public string? AccessToken { get; set; }
        public string? RefreshToken { get; set; }
        public DateTime TokenExpiry { get; set; }

        public bool EnableAdultContent { get; set; } = false;
        public int DefaultItemsPerPage { get; set; } = 20;
        public string PreferredStreamingType { get; set; } = "http"; // http, hls, hls2, hls4
    }
}
