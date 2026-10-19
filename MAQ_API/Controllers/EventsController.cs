using MAQ_API.Dtos;
using MAQ_API.Exceptions;
using MAQ_API.Services;
using Microsoft.AspNetCore.Mvc;

namespace MAQ_API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class EventsController(IEventService service) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<List<EventResponse>>> GetEvents() =>
        Ok((await service.GetAllAsync()).Select(EventResponse.From));

    [HttpGet("{id:int}")]
    public async Task<ActionResult<EventResponse>> GetEventById(int id)
    {
        var ev = await service.GetByIdAsync(id);
        return ev is null ? NotFound() : Ok(EventResponse.From(ev));
    }

    [HttpGet("{id:int}/image")]
    public async Task<IActionResult> GetEventImage(int id)
    {
        var image = await service.GetImageAsync(id);
        return image is null ? NotFound() : File(image.Value.Data, image.Value.ContentType);
    }

    [HttpPost]
    [Consumes("multipart/form-data")]
    public async Task<ActionResult<EventResponse>> PostEvent([FromForm] EventRequest request)
    {
        try
        {
            var ev = await service.CreateAsync(request);
            return CreatedAtAction(nameof(GetEventById), new { id = ev.EventId }, EventResponse.From(ev));
        }
        catch (InvalidImageException ex)
        {
            return BadRequest(new ProblemDetails { Title = "Invalid image", Detail = ex.Message, Status = 400 });
        }
    }

    [HttpPut("{id:int}")]
    [Consumes("multipart/form-data")]
    public async Task<ActionResult<EventResponse>> UpdateEvent(int id, [FromForm] EventRequest request)
    {
        try
        {
            var ev = await service.UpdateAsync(id, request);
            return ev is null ? NotFound() : Ok(EventResponse.From(ev));
        }
        catch (InvalidImageException ex)
        {
            return BadRequest(new ProblemDetails { Title = "Invalid image", Detail = ex.Message, Status = 400 });
        }
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> DeleteEvent(int id) =>
        await service.DeleteAsync(id) ? NoContent() : NotFound();
}
