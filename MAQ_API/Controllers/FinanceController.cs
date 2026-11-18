using MAQ_API.Data;
using MAQ_API.Dtos;
using MAQ_API.Models;
using MAQ_API.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MAQ_API.Controllers;

[ApiController]
[Authorize(Roles = Roles.Admin)]
[Route("api/finance")]
public class FinanceController(IFinanceService service) : ControllerBase
{
    [HttpGet("balance")]
    public async Task<ActionResult<BalanceResponse>> GetBalance(CancellationToken ct) =>
        Ok(await service.GetBalanceAsync(ct));

    [HttpGet("transactions")]
    public async Task<ActionResult<List<FinancialTransactionResponse>>> List(
        [FromQuery] DateOnly? from, [FromQuery] DateOnly? to, [FromQuery] FinanceCategory? category, CancellationToken ct) =>
        Ok(await service.ListTransactionsAsync(from, to, category, ct));

    [HttpPost("transactions")]
    public async Task<ActionResult<FinancialTransactionResponse>> AddManual(ManualIncomeRequest request, CancellationToken ct) =>
        Ok(await service.RecordIncomeAsync(
            request.Amount, request.Category, FinanceSource.Manual, request.Description, ct: ct));
}
