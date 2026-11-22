namespace Nights.Services.Interfaces;

/// <summary>
/// Cloudflare Turnstile CAPTCHA doğrulama servisi arayüzü.
/// </summary>
public interface ITurnstileService
{
    /// <summary>
    /// Ziyaretçinin gönderdiği Turnstile token'ını Cloudflare sunucularında doğrular.
    /// </summary>
    Task<bool> VerifyTokenAsync(string? token, string? remoteIp);
}
