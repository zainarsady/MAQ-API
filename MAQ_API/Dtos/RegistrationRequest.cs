using System.ComponentModel.DataAnnotations;

namespace MAQ_API.Dtos;

public record RegistrationRequest(
    [Required, MaxLength(100)] string FullName,
    [Required, EmailAddress, MaxLength(150)] string Email,
    [Required, MaxLength(20)] string PhoneNumber,
    [Range(1, int.MaxValue)] int NumberOfAttendees,
    [MaxLength(500)] string? Notes);
