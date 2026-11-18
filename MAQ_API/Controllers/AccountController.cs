using MAQ_API.Dtos;
using MAQ_API.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MAQ_API.Controllers;

[ApiController]
[Route("api/account")]
public class AccountController(IAccountService service) : ControllerBase
{
    [HttpPost("login")]
    [AllowAnonymous]
    public async Task<ActionResult<LoginResponse>> Login(LoginRequest request)
    {
        var response = await service.LoginAsync(request);
        return response is null
            ? Unauthorized(new ProblemDetails { Title = "Unauthorized", Detail = "Invalid email or password", Status = 401 })
            : Ok(response);
    }
}
