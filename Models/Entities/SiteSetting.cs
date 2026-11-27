namespace Nights.Models.Entities;

public class SiteSetting
{
    public int Id { get; set; }
    public string GithubUrl { get; set; } = "https://github.com";
    public string LinkedinUrl { get; set; } = "https://linkedin.com";
    public string ContactEmail { get; set; } = "iletisim@domain.local";
    public string QuoteText { get; set; } = "Geçmişin izinde, geleceğin kodunda...";
    public string QuoteAuthor { get; set; } = "DİVAN";
    public string LogoText { get; set; } = "DİVAN";
    public string LogoSubText { get; set; } = "PERSONAL DIGITAL ARCHIVE";
    public string? CustomLogoUrl { get; set; }

    /// <summary>
    /// Arka plan modu: "image" (görsel) veya "color" (yalnızca renk)
    /// </summary>
    public string BackgroundMode { get; set; } = "image";

    /// <summary>
    /// Karanlık Mod için Arka Plan Görseli URL'si (Boşsa varsayılan /images/divan-hero-bg.jpg)
    /// </summary>
    public string? DarkBackgroundImageUrl { get; set; }

    /// <summary>
    /// Aydınlık Mod için Arka Plan Görseli URL'si (Boşsa karanlık moddaki veya varsayılan kullanılır)
    /// </summary>
    public string? LightBackgroundImageUrl { get; set; }

    /// <summary>
    /// Karanlık Mod Arka Plan Rengi (Örn: #080D14)
    /// </summary>
    public string? DarkBackgroundColor { get; set; } = "#080D14";

    /// <summary>
    /// Aydınlık Mod Arka Plan Rengi (Örn: #F8F5EE)
    /// </summary>
    public string? LightBackgroundColor { get; set; } = "#F8F5EE";

    /// <summary>
    /// Arka Plan Bulanıklık (Blur) Değeri (Piksel cinsinden, örn: 6)
    /// </summary>
    public int BackgroundBlur { get; set; } = 6;

    /// <summary>
    /// Arka Plan Karartma / Opaklık Seviyesi (Yüzde cinsinden, 0-100. 100: çok net/az maske, 0: tamamen siyah/beyaz kaplama. Varsayılan: 75)
    /// </summary>
    public int BackgroundOpacity { get; set; } = 75;

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
