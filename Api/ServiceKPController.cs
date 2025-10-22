using System;
using System.Linq;
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

    [Route("/ServiceKP/Devices", "GET", Summary = "Get list of linked devices")]
    public class GetDevicesRequest : IReturn<GetDevicesResponse>
    {
    }

    [Route("/ServiceKP/Device/{Id}/Remove", "POST", Summary = "Remove a device")]
    public class RemoveDeviceRequest : IReturn<RemoveDeviceResponse>
    {
        public string Id { get; set; } = string.Empty;
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

    public class GetDevicesResponse
    {
        public bool Success { get; set; }
        public string? Message { get; set; }
        public System.Collections.Generic.List<DeviceDto>? Devices { get; set; }
    }

    public class DeviceDto
    {
        public string Id { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string Hardware { get; set; } = string.Empty;
        public string Software { get; set; } = string.Empty;
        public long Created { get; set; }
        public long Updated { get; set; }
        public long LastSeen { get; set; }
        public bool IsBrowser { get; set; }
    }

    public class RemoveDeviceResponse
    {
        public bool Success { get; set; }
        public string? Message { get; set; }
        public bool IsCurrent { get; set; }
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

        public async Task<object> Get(GetDevicesRequest request)
        {
            var response = new GetDevicesResponse();

            try
            {
                var plugin = Plugin.Instance;
                var apiClient = plugin?.GetApiClient();

                if (apiClient == null)
                {
                    response.Success = false;
                    response.Message = "Plugin not initialized or not authenticated";
                    return response;
                }

                var devicesResponse = await apiClient.DevicesAsync(CancellationToken.None);

                response.Success = true;
                response.Devices = devicesResponse.Devices.Select(d => new DeviceDto
                {
                    Id = d.Id,
                    Title = d.Title,
                    Hardware = d.Hardware,
                    Software = d.Software,
                    Created = d.Created,
                    Updated = d.Updated,
                    LastSeen = d.LastSeen,
                    IsBrowser = d.IsBrowser == 1
                }).ToList();
            }
            catch (Exception ex)
            {
                _logger.Error($"Error fetching devices: {ex.Message}");
                response.Success = false;
                response.Message = $"Error: {ex.Message}";
            }

            return response;
        }

        public async Task<object> Post(RemoveDeviceRequest request)
        {
            var response = new RemoveDeviceResponse();

            try
            {
                var plugin = Plugin.Instance;
                var apiClient = plugin?.GetApiClient();

                if (apiClient == null)
                {
                    response.Success = false;
                    response.Message = "Plugin not initialized or not authenticated";
                    return response;
                }

                if (string.IsNullOrEmpty(request.Id))
                {
                    response.Success = false;
                    response.Message = "Device ID is required";
                    return response;
                }

                var removeResponse = await apiClient.DeviceRemoveByIdAsync(request.Id, CancellationToken.None);

                response.Success = removeResponse.Error == null;
                response.IsCurrent = removeResponse.Current;
                response.Message = removeResponse.Current
                    ? "Current device removed. You will need to re-authenticate."
                    : "Device removed successfully";

                _logger.Info($"Device {request.Id} removed successfully");
            }
            catch (Exception ex)
            {
                _logger.Error($"Error removing device: {ex.Message}");
                response.Success = false;
                response.Message = $"Error: {ex.Message}";
            }

            return response;
        }
    }
}
