using System.ComponentModel.DataAnnotations;
using MAQ_API.Helpers;

namespace MAQ_API.Dtos;

public record TempahanRamadanRequest(
    [Required] DateOnly? BookedDate,
    [Required, MaxLength(100)] string CustomerName,
    [Required, MaxLength(20)] string ContactNumber, string ICNumber,
    int? Age,
    [MaxLength(100)] List<string>? ArwahName, 
    string? Address,
    [BookingAmount] decimal Amount);
