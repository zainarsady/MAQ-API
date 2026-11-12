using MAQ_API.Dtos;
using MAQ_API.Exceptions;
using MAQ_API.Services;
using Microsoft.AspNetCore.Mvc;

namespace MAQ_API.Controllers;

[ApiController]
[Route("api/payments")]
public class PaymentsController(IPaymentService service) : ControllerBase
{
    [HttpGet("channels")]
    public async Task<ActionResult<List<PaymentChannelResponse>>> GetChannels(CancellationToken ct)
    {
        try { return Ok(await service.GetChannelsAsync(ct)); }
        catch (HerepayException) { return BadGateway(); }
    }

    [HttpPost]
    public async Task<ActionResult<InitiatePaymentResponse>> Create(CreatePaymentRequest request, CancellationToken ct)
    {
        try { return Ok(await service.CreateAsync(request, ct)); }
        catch (BookingNotFoundException ex)
        {
            return NotFound(new ProblemDetails { Title = "Booking not found", Detail = ex.Message, Status = 404 });
        }
        catch (PaymentNotAllowedException ex)
        {
            return Conflict(new ProblemDetails { Title = "Payment not allowed", Detail = ex.Message, Status = 409 });
        }
        catch (HerepayException ex) when (ex.IsClientError)
        {
            return BadRequest(new ProblemDetails { Title = "Payment rejected", Detail = ex.Message, Status = 400 });
        }
        catch (HerepayException) { return BadGateway(); }
    }

    [HttpPost("callback")]
    [Consumes("application/x-www-form-urlencoded")]
    public async Task<IActionResult> Callback(CancellationToken ct)
    {
        var fields = Request.Form.ToDictionary(kv => kv.Key, kv => kv.Value.ToString());
        try
        {
            await service.HandleCallbackAsync(fields, ct);
            return Content("OK", "text/plain");
        }
        catch (InvalidChecksumException ex)
        {
            return Unauthorized(new { message = ex.Message });
        }
    }

    [HttpGet("{paymentCode}")]
    public async Task<ActionResult<PaymentStatusResponse>> GetStatus(string paymentCode, CancellationToken ct)
    {
        var status = await service.GetStatusAsync(paymentCode, ct);
        return status is null ? NotFound() : Ok(status);
    }

    private ObjectResult BadGateway() =>
        StatusCode(502, new ProblemDetails { Title = "Payment gateway error", Detail = "The payment gateway is unavailable.", Status = 502 });
}
