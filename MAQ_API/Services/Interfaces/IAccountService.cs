using MAQ_API.Dtos;

namespace MAQ_API.Services;

public interface IAccountService
{
    /// <summary>Returns null when the email is unknown or the password is wrong (or the account is locked out).</summary>
    Task<LoginResponse?> LoginAsync(LoginRequest request);
}
