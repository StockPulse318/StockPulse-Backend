using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StockPulse.API.DTOs;
using StockPulse.BLL.Interfaces;

namespace StockPulse.API.Controllers;

[ApiController]
[Route("products/{id:int}")]
[Route("api/products/{id:int}")]
[Authorize]
public sealed class StockController : ControllerBase
{
    private readonly IProductService _productService;

    public StockController(IProductService productService)
    {
        _productService = productService;
    }

    /// <summary>POST /products/{id}/stock-in</summary>
    [HttpPost("stock-in")]
    public async Task<IActionResult> StockIn(int id, [FromBody] StockMovementRequest request)
    {
        if (request is null || request.Amount <= 0)
        {
            return BadRequest(new ErrorResponse(new ErrorDetail("VALIDATION_ERROR", "Amount must be a positive integer greater than zero.")));
        }

        var userId = GetUserId();
        var result = await _productService.StockInAsync(id, request.Amount, userId);

        if (result.IsFailure)
        {
            var statusCode = result.ErrorCode switch
            {
                "NOT_FOUND"        => 404,
                "VALIDATION_ERROR" => 400,
                _                  => 400
            };
            return StatusCode(statusCode, new ErrorResponse(new ErrorDetail(result.ErrorCode ?? "BAD_REQUEST", result.ErrorMessage ?? "Stock-In failed.")));
        }

        return Ok(new StockMovementResponse(
            $"{request.Amount} units added to product ID {id}.",
            id,
            request.Amount));
    }

    /// <summary>POST /products/{id}/stock-out</summary>
    [HttpPost("stock-out")]
    public async Task<IActionResult> StockOut(int id, [FromBody] StockMovementRequest request)
    {
        if (request is null || request.Amount <= 0)
        {
            return BadRequest(new ErrorResponse(new ErrorDetail("VALIDATION_ERROR", "Amount must be a positive integer greater than zero.")));
        }

        var userId = GetUserId();
        var result = await _productService.StockOutAsync(id, request.Amount, userId);

        if (result.IsFailure)
        {
            var statusCode = result.ErrorCode switch
            {
                "NOT_FOUND"          => 404,
                "INSUFFICIENT_STOCK" => 409,
                "VALIDATION_ERROR"   => 400,
                _                    => 400
            };
            return StatusCode(statusCode, new ErrorResponse(new ErrorDetail(result.ErrorCode ?? "BAD_REQUEST", result.ErrorMessage ?? "Stock-Out failed.")));
        }

        return Ok(new StockMovementResponse(
            $"{request.Amount} units removed from product ID {id}.",
            id,
            request.Amount));
    }

    private int GetUserId()
    {
        var idClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (int.TryParse(idClaim, out var id))
            return id;

        return 1;
    }
}
