using MAQ_API.Dtos;
using MAQ_API.Exceptions;
using MAQ_API.Models;
using MAQ_API.Services;
using Microsoft.AspNetCore.Mvc;

namespace MAQ_API.Controllers;

[ApiController]
[Route("api/events/{eventId:int}/registrations")]
public class RegistrationsController(IRegistrationService service) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<List<Registration>>> GetRegistrations(int eventId)
    {
        var registrations = await service.GetAllAsync(eventId);
        return registrations is null ? NotFound() : Ok(registrations);
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<Registration>> GetRegistrationById(int eventId, int id)
    {
        var registration = await service.GetByIdAsync(eventId, id);
        return registration is null ? NotFound() : Ok(registration);
    }

    [HttpPost]
    public async Task<ActionResult<Registration>> PostRegistration(int eventId, RegistrationRequest request)
    {
        try
        {
            var registration = await service.CreateAsync(eventId, request);
            return registration is null
                ? NotFound()
                : CreatedAtAction(nameof(GetRegistrationById),
                    new { eventId, id = registration.RegistrationId }, registration);
        }
        catch (EventFullException ex)
        {
            return Conflict(new ProblemDetails { Title = "Registration conflict", Detail = ex.Message, Status = 409 });
        }
    }

    [HttpPut("{id:int}")]
    public async Task<ActionResult<Registration>> UpdateRegistration(int eventId, int id, RegistrationRequest request)
    {
        try
        {
            var registration = await service.UpdateAsync(eventId, id, request);
            return registration is null ? NotFound() : Ok(registration);
        }
        catch (EventFullException ex)
        {
            return Conflict(new ProblemDetails { Title = "Registration conflict", Detail = ex.Message, Status = 409 });
        }
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> DeleteRegistration(int eventId, int id) =>
        await service.DeleteAsync(eventId, id) ? NoContent() : NotFound();
}
