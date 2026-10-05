using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StockPulse.API.DTOs;
using StockPulse.BLL.Interfaces;

namespace StockPulse.API.Controllers;

[ApiController]
[Route("alerts")]
[Route("api/alerts")]
[Authorize]
public sealed class AlertsController : ControllerBase
{
    private readonly IProductService _productService;

    public AlertsController(IProductService productService)
    {
        _productService = productService;
    }

    /// <summary>GET /alerts/low-stock</summary>
    [HttpGet("low-stock")]
    public async Task<IActionResult> GetLowStock()
    {
        var result = await _productService.GetLowStockAsync();

        if (result.IsFailure)
        {
            return StatusCode(500, new ErrorResponse(new ErrorDetail(result.ErrorCode ?? "INTERNAL_ERROR", result.ErrorMessage ?? "Failed to retrieve low-stock alerts.")));
        }

        var response = result.Value!.Select(ProductsController.MapToResponse);
        return Ok(response);
    }
}
