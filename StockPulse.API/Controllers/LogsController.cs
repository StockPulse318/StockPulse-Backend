using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StockPulse.API.DTOs;
using StockPulse.BLL.Interfaces;
using StockPulse.Domain.Entities;

namespace StockPulse.API.Controllers;

[ApiController]
[Route("api/logs")]
[Authorize]
public sealed class LogsController : ControllerBase
{
    private readonly ITransactionService _transactionService;

    public LogsController(ITransactionService transactionService)
    {
        _transactionService = transactionService;
    }

    /// <summary>GET api/logs — Warehouse Manager only</summary>
    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var actorUsername = GetActorUsername();
        var result = await _transactionService.GetAllLogsAsync(actorUsername);

        if (result.IsFailure)
            return Forbid();

        return Ok(result.Value!.Select(MapToResponse));
    }

    /// <summary>GET api/logs/product/{productId}</summary>
    [HttpGet("product/{productId:int}")]
    public async Task<IActionResult> GetByProduct(int productId)
    {
        var actorUsername = GetActorUsername();
        var result = await _transactionService.GetLogsByProductAsync(actorUsername, productId);

        if (result.IsFailure)
            return Forbid();

        return Ok(result.Value!.Select(MapToResponse));
    }

    /// <summary>GET api/logs/user/{username}</summary>
    [HttpGet("user/{username}")]
    public async Task<IActionResult> GetByUser(string username)
    {
        var actorUsername = GetActorUsername();
        var result = await _transactionService.GetLogsByUserAsync(actorUsername, username);

        if (result.IsFailure)
            return Forbid();

        return Ok(result.Value!.Select(MapToResponse));
    }

    private string GetActorUsername() =>
        User.Identity?.Name
            ?? throw new InvalidOperationException("Authenticated user identity is missing from token.");

    private static TransactionLogResponse MapToResponse(InventoryTransactionLog log) =>
        new(log.TransactionID, log.ProductID, log.TransactionType,
            log.QuantityChanged, log.HandledBy, log.Timestamp);
}
