using System.ComponentModel.DataAnnotations;

namespace MAQ_API.Models;

public class Event
{
    [Key]
    public int EventId { get; set; }

    [Required, MaxLength(200)]
    public string Title { get; set; } = string.Empty;

    public DateOnly Date { get; set; }

    public byte[]? Image { get; set; }

    [MaxLength(100)]
    public string? ImageContentType { get; set; }

    public EventType EventType { get; set; }

    public TimeOnly Time { get; set; }

    [Required, MaxLength(200)]
    public string Venue { get; set; } = string.Empty;

    [Required]
    public string Description { get; set; } = string.Empty;

    [Range(1, int.MaxValue)]
    public int Capacity { get; set; }

    public List<Registration> Registrations { get; set; } = [];

    public DateTime CreatedAt { get; set; }
}
