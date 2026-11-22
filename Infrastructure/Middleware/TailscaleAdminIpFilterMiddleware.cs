using Nights.Infrastructure.Security;

namespace Nights.Infrastructure.Middleware;

/// <summary>
/// Zero-Trust Admin Paneli IP Filtreleme Middleware.
/// Admin alanına gelen isteklerin yalnızca Tailscale özel ağı (100.64.0.0/10)
/// veya Localhost üzerinden yapılmasına izin verir.
/// Dış internetten gelen tüm istekleri doğrudan 404 Not Found olarak düşürür.
/// </summary>
public class TailscaleAdminIpFilterMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<TailscaleAdminIpFilterMiddleware> _logger;

    public const string SecretAdminPrefix = "/yonetim-x9k2m";

    public TailscaleAdminIpFilterMiddleware(RequestDelegate next, ILogger<TailscaleAdminIpFilterMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var path = context.Request.Path.Value ?? string.Empty;

        // Hem gizli prefix hem de standart Admin rotası filtrelenir
        if (path.StartsWith(SecretAdminPrefix, StringComparison.OrdinalIgnoreCase) ||
            path.StartsWith("/Admin", StringComparison.OrdinalIgnoreCase))
        {
            var remoteIp = context.Connection.RemoteIpAddress;

            if (!remoteIp.IsTailscaleOrLocalhost())
            {
                _logger.LogWarning("[Zero-Trust Engeli] Yetkisiz Admin erişim teşebbüsü reddedildi! IP: {Ip}, Yol: {Path}", remoteIp, path);
                context.Response.StatusCode = StatusCodes.Status404NotFound;
                return;
            }
        }

        await _next(context);
    }
}
