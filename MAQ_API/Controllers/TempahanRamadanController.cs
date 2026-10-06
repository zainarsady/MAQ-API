using MAQ_API.Dtos;
using MAQ_API.Exceptions;
using MAQ_API.Models;
using MAQ_API.Services;
using Microsoft.AspNetCore.Mvc;

namespace MAQ_API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class TempahanRamadanController(ITempahanRamadanService service) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<List<TempahanRamadan>>> GetTempahanRamadan() =>
        Ok(await service.GetAllAsync());

    [HttpGet("{id:int}")]
    public async Task<ActionResult<TempahanRamadan>> GetTempahanRamadanById(int id)
    {
        var booking = await service.GetByIdAsync(id);
        return booking is null ? NotFound() : Ok(booking);
    }

    [HttpPost]
    public async Task<ActionResult<TempahanRamadan>> PostTempahanRamadan(TempahanRamadanRequest request)
    {
        try
        {
            var booking = await service.CreateAsync(request);
            return CreatedAtAction(nameof(GetTempahanRamadanById), new { id = booking.TempahanRamadanId }, booking);
        }
        catch (Exception ex) when (ex is DuplicateBookingException or DailyLimitReachedException)
        {
            return Conflict(new ProblemDetails { Title = "Booking conflict", Detail = ex.Message, Status = 409 });
        }
    }

    [HttpPut("{id:int}")]
    public async Task<ActionResult<TempahanRamadan>> UpdateTempahanRamadan(int id, TempahanRamadanRequest request)
    {
        try
        {
            var booking = await service.UpdateAsync(id, request);
            return booking is null ? NotFound() : Ok(booking);
        }
        catch (Exception ex) when (ex is DuplicateBookingException or DailyLimitReachedException)
        {
            return Conflict(new ProblemDetails { Title = "Booking conflict", Detail = ex.Message, Status = 409 });
        }
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> DeleteTempahanRamadan(int id) =>
        await service.DeleteAsync(id) ? NoContent() : NotFound();
}
