using System.ComponentModel.DataAnnotations;
using Nights.Models.Entities;

namespace Nights.Models.ViewModels;

public class MemoryListViewModel
{
    public List<Memory> Memories { get; set; } = new();
}

public class MemorySubmitInputModel
{
    [Required(ErrorMessage = "Açıklama alanı zorunludur.")]
    [StringLength(280, MinimumLength = 3, ErrorMessage = "Açıklama 3 ile 280 karakter arasında olmalıdır.")]
    public string Description { get; set; } = string.Empty;

    [Required(ErrorMessage = "Lütfen bir fotoğraf seçiniz.")]
    public IFormFile? File { get; set; }

    /// <summary>
    /// Cloudflare Turnstile token değeri (Bot koruması)
    /// </summary>
    public string? TurnstileToken { get; set; }
}
