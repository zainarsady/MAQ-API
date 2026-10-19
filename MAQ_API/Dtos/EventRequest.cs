using System.ComponentModel.DataAnnotations;
using MAQ_API.Models;

namespace MAQ_API.Dtos;

public record EventRequest(
    [Required, MaxLength(200)] string Title,
    [Required] DateOnly? Date,
    [Required] EventType? EventType,
    [Required] TimeOnly? Time,
    [Required, MaxLength(200)] string Venue,
    [Required] string Description,
    [Range(1, int.MaxValue)] int Capacity,
    IFormFile? Image);
