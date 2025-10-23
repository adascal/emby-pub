using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Common.Net;
using MediaBrowser.Common.Plugins;
using MediaBrowser.Model.Logging;
using MediaBrowser.Model.Plugins;
using MediaBrowser.Model.Serialization;
using ServiceKP.Plugin.Api;
using ServiceKP.Plugin.Configuration;
using ServiceKP.Plugin.Models;

namespace ServiceKP.Plugin
{
    public class Plugin : BasePlugin<PluginConfiguration>, IHasWebPages
    {
        private readonly IHttpClient _httpClient;
        private readonly ILogger _logger;
        private ServiceKPApiClient? _apiClient;

        public Plugin(IApplicationPaths applicationPaths, IXmlSerializer xmlSerializer, IHttpClient httpClient, ILogManager logManager)
            : base(applicationPaths, xmlSerializer)
        {
            _httpClient = httpClient;
            _logger = logManager.GetLogger(Name);
            Instance = this;

            _logger.Info("ServiceKP Plugin initialized");
        }

        public static Plugin? Instance { get; private set; }

        public override string Name => "ServiceKP";

        public override string Description => "Stream movies and TV shows from ServiceKP";

        public override Guid Id => Guid.Parse("12345678-1234-1234-1234-123456789abc");

        public ServiceKPApiClient GetApiClient()
        {
            if (_apiClient == null)
            {
                _apiClient = new ServiceKPApiClient(
                    _httpClient,
                    _logger,
                    Configuration.ApiBaseUrl,
                    Configuration.ClientId,
                    Configuration.ClientSecret
                );

                // Set callback to save tokens when they are refreshed
                _apiClient.SetTokensRefreshedCallback((accessToken, refreshToken, expiry) =>
                {
                    Configuration.AccessToken = accessToken;
                    Configuration.RefreshToken = refreshToken;
                    Configuration.TokenExpiry = expiry;
                    SaveConfiguration();
                    _logger.Info("Refreshed tokens saved to configuration");
                });

                // Restore tokens if available
                if (!string.IsNullOrEmpty(Configuration.AccessToken))
                {
                    _apiClient.SetTokens(
                        Configuration.AccessToken,
                        Configuration.RefreshToken ?? string.Empty,
                        Configuration.TokenExpiry
                    );
                }
            }

            return _apiClient;
        }

        public async Task<bool> AuthenticateAsync(Action<string, string>? onUserCodeRequest = null, CancellationToken cancellationToken = default)
        {
            var apiClient = GetApiClient();

            try
            {
                // Try to use existing tokens first
                if (!string.IsNullOrEmpty(Configuration.AccessToken))
                {
                    try
                    {
                        var userResponse = await apiClient.GetUserAsync(cancellationToken);
                        if (userResponse?.User?.Username != null)
                        {
                            _logger.Info($"Authenticated as {userResponse.User.Username}");
                            return true;
                        }
                    }
                    catch
                    {
                        // Token might be expired, continue with device flow
                    }
                }

                // Try refresh token
                if (!string.IsNullOrEmpty(Configuration.RefreshToken))
                {
                    try
                    {
                        var tokensResponse = await apiClient.RefreshTokensAsync(Configuration.RefreshToken, cancellationToken);
                        if (tokensResponse?.Error == null && tokensResponse != null)
                        {
                            SaveTokens(tokensResponse);
                            return true;
                        }
                    }
                    catch
                    {
                        // Refresh failed, continue with device flow
                    }
                }

                // Start device flow
                var deviceCodeResponse = await apiClient.RequestDeviceCodeAsync(cancellationToken);

                if (!string.IsNullOrEmpty(deviceCodeResponse.UserCode))
                {
                    _logger.Info($"Device code: {deviceCodeResponse.UserCode}");
                    _logger.Info($"Verification URL: {deviceCodeResponse.VerificationUri}");

                    onUserCodeRequest?.Invoke(deviceCodeResponse.UserCode, deviceCodeResponse.VerificationUri);

                    // Poll for authorization
                    var interval = deviceCodeResponse.Interval > 0 ? deviceCodeResponse.Interval : 10;
                    var maxAttempts = 60; // 10 minutes max
                    var attempts = 0;

                    while (attempts < maxAttempts && !cancellationToken.IsCancellationRequested)
                    {
                        await Task.Delay(interval * 1000, cancellationToken);

                        var tokensResponse = await apiClient.RequestTokensAsync(deviceCodeResponse.Code, cancellationToken);

                        if (tokensResponse.Error == null && !string.IsNullOrEmpty(tokensResponse.AccessToken))
                        {
                            SaveTokens(tokensResponse);

                            // Notify device
                            var deviceInfo = new DeviceInfo
                            {
                                Title = "Emby Server",
                                Hardware = Environment.OSVersion.Platform.ToString(),
                                Software = $"Emby {typeof(Plugin).Assembly.GetName().Version}"
                            };

                            await apiClient.DeviceNotifyAsync(deviceInfo, cancellationToken);

                            _logger.Info("Authentication successful");
                            return true;
                        }

                        if (tokensResponse.Error == "authorization_expired" || tokensResponse.Error == "invalid_refresh_token")
                        {
                            _logger.Error($"Authentication failed: {tokensResponse.Error}");
                            return false;
                        }

                        attempts++;
                    }
                }

                return false;
            }
            catch (Exception ex)
            {
                _logger.Error($"Authentication error: {ex.Message}");
                return false;
            }
        }

        private void SaveTokens(TokensResponse response)
        {
            Configuration.AccessToken = response.AccessToken;
            Configuration.RefreshToken = response.RefreshToken;
            Configuration.TokenExpiry = DateTime.UtcNow.AddSeconds(response.ExpiresIn);
            SaveConfiguration();

            var apiClient = GetApiClient();
            apiClient.SetTokens(response.AccessToken, response.RefreshToken, Configuration.TokenExpiry);
        }

        public void ClearAuthentication()
        {
            Configuration.AccessToken = null;
            Configuration.RefreshToken = null;
            Configuration.TokenExpiry = DateTime.MinValue;
            SaveConfiguration();

            var apiClient = GetApiClient();
            apiClient.ClearTokens();
        }

        public IEnumerable<PluginPageInfo> GetPages()
        {
            return new[]
            {
                new PluginPageInfo
                {
                    Name = Name,
                    EmbeddedResourcePath = GetType().Namespace + ".Configuration.configPage.html"
                }
            };
        }
    }
}
