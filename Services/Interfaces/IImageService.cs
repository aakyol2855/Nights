namespace Nights.Services.Interfaces;

public interface IImageService
{
    Task<string> ProcessAndSavePendingAsync(IFormFile file);
    Task<bool> ApproveImageAsync(string fileName);
    void DeleteImage(string fileName, bool isPending);
    (byte[] fileBytes, string contentType)? GetImageStream(string fileName, bool isPending);

    // Çöp Kutusu (Karantina) dosya yönetimi
    bool MoveToTrash(string fileName, bool isPending);
    bool RestoreFromTrash(string fileName, bool restoreToApproved);
    void DeleteFromTrash(string fileName);
    (byte[] fileBytes, string contentType)? GetTrashImageStream(string fileName);
}
