using MAQ_API.Data;
using MAQ_API.Dtos;
using MAQ_API.Exceptions;
using MAQ_API.Models;
using MAQ_API.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MAQ_API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class TempahanRamadanController(ITempahanRamadanService service) : ControllerBase
{
    [HttpGet]
    [Authorize(Roles = Roles.Admin)]
    public async Task<ActionResult<List<TempahanRamadanResponse>>> GetTempahanRamadan() =>
        Ok(await service.GetAllAsync());

    /// <summary>Public list of names for a date, limited to bookings with a successful payment.</summary>
    [HttpGet("donors")]
    [AllowAnonymous]
    public async Task<ActionResult<List<DonorResponse>>> GetDonors([FromQuery] DateOnly? date)
    {
        if (date is null)
            return BadRequest(new ProblemDetails { Title = "Invalid date", Detail = "Query parameter 'date' is required (yyyy-MM-dd).", Status = 400 });

        return Ok(await service.GetPaidDonorsAsync(date.Value));
    }

    [HttpGet("availability")]
    public async Task<ActionResult<List<DateAvailabilityResponse>>> GetAvailability(
        [FromQuery] DateOnly? from, [FromQuery] DateOnly? to)
    {
        if (from is not null && to is not null)
        {
            if (from > to)
                return BadRequest(new ProblemDetails { Title = "Invalid range", Detail = "'from' must not be after 'to'.", Status = 400 });
            if (to.Value.DayNumber - from.Value.DayNumber >= TempahanRamadanService.MaxAvailabilityRangeDays)
                return BadRequest(new ProblemDetails { Title = "Invalid range", Detail = $"Range must not exceed {TempahanRamadanService.MaxAvailabilityRangeDays} days.", Status = 400 });
        }
        return Ok(await service.GetAvailabilityAsync(from, to));
    }

    [HttpGet("{id:int}")]
    [Authorize(Roles = Roles.Admin)]
    public async Task<ActionResult<TempahanRamadanResponse>> GetTempahanRamadanById(int id)
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
        catch (Exception ex) when (ex is DailyLimitReachedException)
        {
            return Conflict(new ProblemDetails { Title = "Booking conflict", Detail = ex.Message, Status = 409 });
        }
    }

    [HttpPut("{id:int}")]
    [Authorize(Roles = Roles.Admin)]
    public async Task<ActionResult<TempahanRamadan>> UpdateTempahanRamadan(int id, TempahanRamadanRequest request)
    {
        try
        {
            var booking = await service.UpdateAsync(id, request);
            return booking is null ? NotFound() : Ok(booking);
        }
        catch (Exception ex) when (ex is DailyLimitReachedException)
        {
            return Conflict(new ProblemDetails { Title = "Booking conflict", Detail = ex.Message, Status = 409 });
        }
    }

    [HttpDelete("{id:int}")]
    [Authorize(Roles = Roles.Admin)]
    public async Task<IActionResult> DeleteTempahanRamadan(int id) =>
        await service.DeleteAsync(id) ? NoContent() : NotFound();
}
