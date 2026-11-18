using System.ComponentModel.DataAnnotations;

namespace MAQ_API.Dtos;

public record LoginRequest([Required, EmailAddress] string Email, [Required] string Password);

public record LoginResponse(string Token, DateTime ExpiresAt);
