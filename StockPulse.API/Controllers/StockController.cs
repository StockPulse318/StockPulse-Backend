using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StockPulse.API.DTOs;
using StockPulse.BLL.Interfaces;

namespace StockPulse.API.Controllers;

[ApiController]
[Route("api/products/{productId:int}/stock")]
[Authorize]
public sealed class StockController : ControllerBase
{
    private readonly ITransactionService _transactionService;

    public StockController(ITransactionService transactionService)
    {
        _transactionService = transactionService;
    }

    /// <summary>POST api/products/{productId}/stock/in</summary>
    [HttpPost("in")]
    public async Task<IActionResult> StockIn(int productId, [FromBody] StockMovementRequest request)
    {
        var actorUsername = GetActorUsername();
        var result = await _transactionService.StockInAsync(actorUsername, productId, request.Quantity);

        if (result.IsFailure)
            return BadRequest(new { error = result.ErrorMessage });

        return Ok(new { message = $"{request.Quantity} units added to product {productId}." });
    }

    /// <summary>POST api/products/{productId}/stock/out</summary>
    [HttpPost("out")]
    public async Task<IActionResult> StockOut(int productId, [FromBody] StockMovementRequest request)
    {
        var actorUsername = GetActorUsername();
        var result = await _transactionService.StockOutAsync(actorUsername, productId, request.Quantity);

        // 409 Conflict is the correct status for a business rule violation on a valid request —
        // the request itself is well-formed, but the current state of the resource prevents it.
        if (result.IsFailure)
            return Conflict(new { error = result.ErrorMessage });

        return Ok(new { message = $"{request.Quantity} units removed from product {productId}." });
    }

    private string GetActorUsername() =>
        User.Identity?.Name
            ?? throw new InvalidOperationException("Authenticated user identity is missing from token.");
}
