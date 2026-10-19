using System.ComponentModel.DataAnnotations;

namespace MAQ_API.Models;

public class Registration
{
    [Key]
    public int RegistrationId { get; set; }

    public int EventId { get; set; }

    [Required, MaxLength(100)]
    public string FullName { get; set; } = string.Empty;

    [Required, EmailAddress, MaxLength(150)]
    public string Email { get; set; } = string.Empty;

    [Required, MaxLength(20)]
    public string PhoneNumber { get; set; } = string.Empty;

    [Range(1, int.MaxValue)]
    public int NumberOfAttendees { get; set; }

    [MaxLength(500)]
    public string? Notes { get; set; }

    public DateTime CreatedAt { get; set; }
}
