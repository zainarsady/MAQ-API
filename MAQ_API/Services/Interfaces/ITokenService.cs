using Microsoft.AspNetCore.Identity;

namespace MAQ_API.Services;

public interface ITokenService
{
    (string Token, DateTime ExpiresAt) CreateToken(IdentityUser user, IList<string> roles);
}
