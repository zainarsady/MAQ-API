using System.ComponentModel.DataAnnotations;

namespace MAQ_API.Models;

public class News
{
    [Key]
    public int NewsId { get; set; }

    [Required, MaxLength(200)]
    public string Title { get; set; } = string.Empty;

    public EventType Type { get; set; }

    public byte[]? Image { get; set; }

    [MaxLength(100)]
    public string? ImageContentType { get; set; }

    [Required]
    public string BlogContent { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; }
}
