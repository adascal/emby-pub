using System;
using System.Threading;
using System.Threading.Tasks;
using MediaBrowser.Model.Logging;
using MediaBrowser.Model.Services;

namespace ServiceKP.Plugin.Api
{
    [Route("/ServiceKP/Authenticate", "GET", Summary = "Authenticate with ServiceKP")]
    public class AuthenticateRequest : IReturn<AuthenticateResponse>
    {
    }

    [Route("/ServiceKP/ClearAuth", "GET", Summary = "Clear ServiceKP authentication")]
    public class ClearAuthRequest : IReturn<ClearAuthResponse>
    {
    }

    public class AuthenticateResponse
    {
        public bool Success { get; set; }
        public string? Message { get; set; }
        public string? UserCode { get; set; }
        public string? VerificationUri { get; set; }
    }

    public class ClearAuthResponse
    {
        public bool Success { get; set; }
    }

    public class ServiceKPController : IService
    {
        private readonly ILogger _logger;

        public ServiceKPController(ILogManager logManager)
        {
            _logger = logManager.GetLogger(GetType().Name);
        }

        public async Task<object> Get(AuthenticateRequest request)
        {
            var response = new AuthenticateResponse();

            try
            {
                var plugin = Plugin.Instance;
                if (plugin == null)
                {
                    response.Success = false;
                    response.Message = "Plugin not initialized";
                    return response;
                }

                string? userCode = null;
                string? verificationUri = null;

                var authenticated = await plugin.AuthenticateAsync(
                    (code, uri) =>
                    {
                        userCode = code;
                        verificationUri = uri;
                        _logger.Info($"===========================================");
                        _logger.Info($"ServiceKP Authentication Required");
                        _logger.Info($"===========================================");
                        _logger.Info($"Please visit: {uri}");
                        _logger.Info($"Enter code: {code}");
                        _logger.Info($"===========================================");
                    },
                    CancellationToken.None
                );

                response.Success = authenticated;
                response.UserCode = userCode;
                response.VerificationUri = verificationUri;

                if (authenticated)
                {
                    response.Message = "Authentication successful";
                    _logger.Info("ServiceKP authentication completed successfully");
                }
                else
                {
                    response.Message = "Authentication failed or timed out";
                    _logger.Warn("ServiceKP authentication failed");
                }
            }
            catch (Exception ex)
            {
                _logger.Error($"Authentication error: {ex.Message}");
                response.Success = false;
                response.Message = $"Error: {ex.Message}";
            }

            return response;
        }

        public object Get(ClearAuthRequest request)
        {
            var plugin = Plugin.Instance;
            if (plugin != null)
            {
                plugin.ClearAuthentication();
                _logger.Info("ServiceKP authentication cleared");
            }

            return new ClearAuthResponse { Success = true };
        }
    }
}
