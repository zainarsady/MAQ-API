using MAQ_API.Dtos;
using MAQ_API.Exceptions;
using MAQ_API.Services;
using Microsoft.AspNetCore.Mvc;

namespace MAQ_API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class NewsController(INewsService service) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<List<NewsResponse>>> GetNews() =>
        Ok((await service.GetAllAsync()).Select(NewsResponse.From));

    [HttpGet("{id:int}")]
    public async Task<ActionResult<NewsResponse>> GetNewsById(int id)
    {
        var news = await service.GetByIdAsync(id);
        return news is null ? NotFound() : Ok(NewsResponse.From(news));
    }

    [HttpGet("{id:int}/image")]
    public async Task<IActionResult> GetNewsImage(int id)
    {
        var image = await service.GetImageAsync(id);
        return image is null ? NotFound() : File(image.Value.Data, image.Value.ContentType);
    }

    [HttpPost]
    [Consumes("multipart/form-data")]
    public async Task<ActionResult<NewsResponse>> PostNews([FromForm] NewsRequest request)
    {
        try
        {
            var news = await service.CreateAsync(request);
            return CreatedAtAction(nameof(GetNewsById), new { id = news.NewsId }, NewsResponse.From(news));
        }
        catch (InvalidImageException ex)
        {
            return BadRequest(new ProblemDetails { Title = "Invalid image", Detail = ex.Message, Status = 400 });
        }
    }

    [HttpPut("{id:int}")]
    [Consumes("multipart/form-data")]
    public async Task<ActionResult<NewsResponse>> UpdateNews(int id, [FromForm] NewsRequest request)
    {
        try
        {
            var news = await service.UpdateAsync(id, request);
            return news is null ? NotFound() : Ok(NewsResponse.From(news));
        }
        catch (InvalidImageException ex)
        {
            return BadRequest(new ProblemDetails { Title = "Invalid image", Detail = ex.Message, Status = 400 });
        }
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> DeleteNews(int id) =>
        await service.DeleteAsync(id) ? NoContent() : NotFound();
}
