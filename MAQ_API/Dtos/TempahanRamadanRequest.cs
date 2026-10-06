using System.ComponentModel.DataAnnotations;

namespace MAQ_API.Dtos;

public record TempahanRamadanRequest(
    [Required] DateOnly? BookedDate,
    [Required, MaxLength(100)] string CustomerName,
    [Required, MaxLength(20)] string ContactNumber,
    [Required, MaxLength(20)] string ICNumber,
    [Range(1, 120)] int Age,
    [Required, MaxLength(200)] string Address,
    [Range(0, 99999999.99)] decimal Amount);
