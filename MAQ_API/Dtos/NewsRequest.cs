using System.ComponentModel.DataAnnotations;
using MAQ_API.Models;

namespace MAQ_API.Dtos;

public record NewsRequest(
    [Required, MaxLength(200)] string Title,
    [Required] EventType? Type,
    [Required] string BlogContent,
    IFormFile? Image);
