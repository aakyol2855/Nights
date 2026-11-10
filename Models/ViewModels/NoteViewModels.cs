using System.ComponentModel.DataAnnotations;
using Nights.Models.Entities;

namespace Nights.Models.ViewModels;

public class NoteListViewModel
{
    public List<Note> Notes { get; set; } = new();
    public bool CanCreate { get; set; }
    public bool IsAdmin { get; set; }
    public int CurrentPage { get; set; } = 1;
    public int TotalPages { get; set; } = 1;
    public int TotalCount { get; set; } = 0;
}

public class NoteCreateInputModel
{
    [Required(ErrorMessage = "Başlık alanı zorunludur.")]
    [StringLength(150, MinimumLength = 2, ErrorMessage = "Başlık 2 ile 150 karakter arasında olmalıdır.")]
    public string Title { get; set; } = string.Empty;

    [Required(ErrorMessage = "Not içeriği boş bırakılamaz.")]
    public string Content { get; set; } = string.Empty;

    public bool IsPinned { get; set; } = false;
}

public class NoteEditInputModel
{
    public int Id { get; set; }

    [Required(ErrorMessage = "Başlık alanı zorunludur.")]
    [StringLength(150, MinimumLength = 2, ErrorMessage = "Başlık 2 ile 150 karakter arasında olmalıdır.")]
    public string Title { get; set; } = string.Empty;

    [Required(ErrorMessage = "Not içeriği boş bırakılamaz.")]
    public string Content { get; set; } = string.Empty;

    public bool IsPinned { get; set; } = false;
}

public class UserPermissionViewModel
{
    public string UserId { get; set; } = string.Empty;
    public string UserName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public bool HasNoteAccess { get; set; }
    public bool IsAdmin { get; set; }
}
