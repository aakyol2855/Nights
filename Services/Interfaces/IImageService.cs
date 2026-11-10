namespace Nights.Services.Interfaces;

public interface IImageService
{
    Task<string> ProcessAndSavePendingAsync(IFormFile file);
    Task<bool> ApproveImageAsync(string fileName);
    void DeleteImage(string fileName, bool isPending);
    (byte[] fileBytes, string contentType)? GetImageStream(string fileName, bool isPending);
}
