using Nights.Models.Entities;

namespace Nights.Services.Interfaces;

public interface INoteService
{
    Task<(List<Note> notes, int totalCount, int totalPages)> GetNotesPagedAsync(int page = 1, int pageSize = 15);
    Task<Note?> GetByIdAsync(int id);
    Task<(bool success, string message, Note? note)> CreateNoteAsync(string title, string content, bool isPinned, string userId, string authorName, bool isAdmin);
    Task<(bool success, string message)> UpdateNoteAsync(int id, string title, string content, bool isPinned, string userId, bool isAdmin);
    Task<(bool success, string message)> DeleteNoteAsync(int id, string userId, bool isAdmin);
    Task<int> GetTotalCountAsync();
    int CountWords(string text);
}
