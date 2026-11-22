using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Nights.Services.Interfaces;

namespace Nights.Services.Implementations;

/// <summary>
/// Cloudflare Turnstile API doğrulama servisi.
/// </summary>
public class TurnstileService : ITurnstileService
{
    private readonly HttpClient _httpClient;
    private readonly string _secretKey;
    private readonly bool _isEnabled;
    private readonly ILogger<TurnstileService> _logger;

    public TurnstileService(HttpClient httpClient, IConfiguration configuration, ILogger<TurnstileService> logger)
    {
        _httpClient = httpClient;
        _logger = logger;

        // Varsayılan olarak Cloudflare'in resmi her zaman başarılı dönen "Always Passes" test anahtarı kullanılır
        _secretKey = configuration["CloudflareTurnstile:SecretKey"] ?? "1x0000000000000000000000000000000AA";
        _isEnabled = configuration.GetValue("CloudflareTurnstile:Enabled", true);
    }

    public async Task<bool> VerifyTokenAsync(string? token, string? remoteIp)
    {
        if (!_isEnabled)
        {
            return true;
        }

        if (string.IsNullOrWhiteSpace(token))
        {
            _logger.LogWarning("[Turnstile] Boş token ile doğrulama yapılmaya çalışıldı.");
            return false;
        }

        try
        {
            var content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["secret"] = _secretKey,
                ["response"] = token,
                ["remoteip"] = remoteIp ?? string.Empty
            });

            var response = await _httpClient.PostAsync("https://challenges.cloudflare.com/turnstile/v0/siteverify", content);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("[Turnstile] Cloudflare API HTTP {StatusCode} döndü.", response.StatusCode);
                return false;
            }

            var json = await response.Content.ReadAsStringAsync();
            var result = JsonSerializer.Deserialize<TurnstileVerificationResult>(json);

            if (result?.Success == true)
            {
                return true;
            }

            _logger.LogWarning("[Turnstile] Doğrulama başarısız. Hata kodları: {Errors}", 
                result?.ErrorCodes != null ? string.Join(", ", result.ErrorCodes) : "Yok");

            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[Turnstile] Cloudflare Turnstile servisine erişilirken beklenmeyen hata oluştu.");
            return false;
        }
    }

    private sealed class TurnstileVerificationResult
    {
        [JsonPropertyName("success")]
        public bool Success { get; set; }

        [JsonPropertyName("error-codes")]
        public string[]? ErrorCodes { get; set; }
    }
}
