using Nights.Services.Interfaces;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Webp;
using SixLabors.ImageSharp.Processing;

namespace Nights.Services.Implementations;

public class ImageService : IImageService
{
    private readonly string _pendingPath;
    private readonly string _approvedPath;
    private static readonly string[] AllowedExtensions = [".jpg", ".jpeg", ".png", ".webp"];

    public ImageService(IWebHostEnvironment env)
    {
        var baseStorage = Path.Combine(env.ContentRootPath, "Storage", "uploads");
        _pendingPath = Path.Combine(baseStorage, "pending");
        _approvedPath = Path.Combine(baseStorage, "approved");

        Directory.CreateDirectory(_pendingPath);
        Directory.CreateDirectory(_approvedPath);
    }

    public async Task<string> ProcessAndSavePendingAsync(IFormFile file)
    {
        if (file == null || file.Length == 0 || file.Length > 5 * 1024 * 1024)
            throw new InvalidOperationException("Geçersiz dosya boyutu (Maksimum 5MB).");

        var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (!AllowedExtensions.Contains(extension))
            throw new InvalidOperationException("Yalnızca JPG, PNG ve WebP formatları desteklenmektedir.");

        using var stream = file.OpenReadStream();
        if (!IsValidImageHeader(stream, extension))
            throw new InvalidOperationException("Dosya başlığı (Magic Bytes) geçersiz veya manipüle edilmiş.");

        stream.Position = 0;

        var safeFileName = $"{Guid.NewGuid():N}.webp";
        var targetFilePath = Path.Combine(_pendingPath, safeFileName);

        using (var image = await Image.LoadAsync(stream))
        {
            // EXIF ve metadata verilerini tamamen kaldır
            image.Metadata.ExifProfile = null;
            image.Metadata.IptcProfile = null;
            image.Metadata.XmpProfile = null;

            // Maksimum 1600x1200 sınırlandırması (Düşük sunucu belleği koruması)
            if (image.Width > 1600 || image.Height > 1200)
            {
                image.Mutate(x => x.Resize(new ResizeOptions
                {
                    Mode = ResizeMode.Max,
                    Size = new Size(1600, 1200)
                }));
            }

            var encoder = new WebpEncoder { Quality = 75 };
            await image.SaveAsWebpAsync(targetFilePath, encoder);
        }

        return safeFileName;
    }

    public Task<bool> ApproveImageAsync(string fileName)
    {
        fileName = Path.GetFileName(fileName);
        var sourcePath = Path.Combine(_pendingPath, fileName);
        var destPath = Path.Combine(_approvedPath, fileName);

        if (!File.Exists(sourcePath)) return Task.FromResult(false);

        File.Move(sourcePath, destPath, overwrite: true);
        return Task.FromResult(true);
    }

    public void DeleteImage(string fileName, bool isPending)
    {
        fileName = Path.GetFileName(fileName);
        var path = Path.Combine(isPending ? _pendingPath : _approvedPath, fileName);
        if (File.Exists(path))
        {
            File.Delete(path);
        }
    }

    public (byte[] fileBytes, string contentType)? GetImageStream(string fileName, bool isPending)
    {
        fileName = Path.GetFileName(fileName);
        var path = Path.Combine(isPending ? _pendingPath : _approvedPath, fileName);
        if (!File.Exists(path)) return null;

        return (File.ReadAllBytes(path), "image/webp");
    }

    private static bool IsValidImageHeader(Stream stream, string extension)
    {
        var header = new byte[12];
        var bytesRead = stream.Read(header, 0, header.Length);
        if (bytesRead < 4) return false;

        // JPEG: FF D8 FF
        if (extension is ".jpg" or ".jpeg")
        {
            return header[0] == 0xFF && header[1] == 0xD8 && header[2] == 0xFF;
        }

        // PNG: 89 50 4E 47 0D 0A 1A 0A
        if (extension is ".png")
        {
            return bytesRead >= 8 &&
                   header[0] == 0x89 && header[1] == 0x50 && header[2] == 0x4E && header[3] == 0x47 &&
                   header[4] == 0x0D && header[5] == 0x0A && header[6] == 0x1A && header[7] == 0x0A;
        }

        // WEBP: RIFF....WEBP (52 49 46 46 .... 57 45 42 50)
        if (extension is ".webp")
        {
            return bytesRead >= 12 &&
                   header[0] == 0x52 && header[1] == 0x49 && header[2] == 0x46 && header[3] == 0x46 &&
                   header[8] == 0x57 && header[9] == 0x45 && header[10] == 0x42 && header[11] == 0x50;
        }

        return false;
    }
}
