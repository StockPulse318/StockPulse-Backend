using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StockPulse.API.DTOs;
using StockPulse.BLL.Interfaces;
using StockPulse.Domain.Entities;

namespace StockPulse.API.Controllers;

[ApiController]
[Route("products")]
[Route("api/products")]
[Authorize]
public sealed class ProductsController : ControllerBase
{
    private readonly IProductService _productService;

    public ProductsController(IProductService productService)
    {
        _productService = productService;
    }

    /// <summary>GET /products</summary>
    [HttpGet]
    public async Task<IActionResult> GetAll(
        [FromQuery] int page = 1,
        [FromQuery] int limit = 25,
        [FromQuery] string? sortBy = "name",
        [FromQuery] string? sortOrder = "asc",
        [FromQuery] int? category_id = null,
        [FromQuery] string? q = null)
    {
        var result = await _productService.GetPagedAsync(page, limit, sortBy, sortOrder, category_id, q);

        if (result.IsFailure)
        {
            return StatusCode(500, new ErrorResponse(new ErrorDetail(result.ErrorCode ?? "INTERNAL_ERROR", result.ErrorMessage ?? "Failed to retrieve products.")));
        }

        var (items, total) = result.Value;
        var clampedLimit = Math.Clamp(limit, 1, 100);
        var totalPages = (int)Math.Ceiling((double)total / clampedLimit);

        var response = new PagedProductsResponse(
            items.Select(MapToResponse),
            total,
            Math.Max(1, page),
            clampedLimit,
            totalPages);

        return Ok(response);
    }

    /// <summary>GET /products/{id}</summary>
    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetById(int id)
    {
        var result = await _productService.GetByIdAsync(id);

        if (result.IsFailure)
        {
            return NotFound(new ErrorResponse(new ErrorDetail("NOT_FOUND", result.ErrorMessage ?? $"Product with ID {id} not found.")));
        }

        return Ok(MapToResponse(result.Value!));
    }

    /// <summary>POST /products — Warehouse Manager only</summary>
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateProductRequest request)
    {
        var role = GetUserRole();
        if (role != UserRoles.WarehouseManager)
        {
            return StatusCode(403, new ErrorResponse(new ErrorDetail("FORBIDDEN", "Only warehouse managers can add products.")));
        }

        if (request is null)
        {
            return BadRequest(new ErrorResponse(new ErrorDetail("VALIDATION_ERROR", "Request body cannot be null.")));
        }

        var product = new Product
        {
            ProductCode  = request.ProductCode,
            Name         = request.Name,
            CategoryId   = request.CategoryId,
            Quantity     = request.Quantity,
            UnitPrice    = request.UnitPrice,
            ReorderLevel = request.ReorderLevel
        };

        var result = await _productService.AddProductAsync(role, product);

        if (result.IsFailure)
        {
            var statusCode = result.ErrorCode switch
            {
                "DUPLICATE_PRODUCT_CODE" => 409,
                "CONFLICT"               => 409,
                "FORBIDDEN"              => 403,
                "VALIDATION_ERROR"       => 400,
                _                        => 400
            };
            return StatusCode(statusCode, new ErrorResponse(new ErrorDetail(result.ErrorCode ?? "BAD_REQUEST", result.ErrorMessage ?? "Failed to create product.")));
        }

        var created = MapToResponse(result.Value!);
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
    }

    /// <summary>PUT /products/{id} — Warehouse Manager only</summary>
    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, [FromBody] UpdateProductRequest request)
    {
        var role = GetUserRole();
        if (role != UserRoles.WarehouseManager)
        {
            return StatusCode(403, new ErrorResponse(new ErrorDetail("FORBIDDEN", "Only warehouse managers can modify products.")));
        }

        if (request is null)
        {
            return BadRequest(new ErrorResponse(new ErrorDetail("VALIDATION_ERROR", "Request body cannot be null.")));
        }

        // Fetch existing quantity to preserve it (quantity only mutates through stock operations)
        var existingResult = await _productService.GetByIdAsync(id);
        if (existingResult.IsFailure)
        {
            return NotFound(new ErrorResponse(new ErrorDetail("NOT_FOUND", $"Product with ID {id} does not exist.")));
        }

        var product = new Product
        {
            Id           = id,
            ProductCode  = request.ProductCode,
            Name         = request.Name,
            CategoryId   = request.CategoryId,
            Quantity     = existingResult.Value!.Quantity,
            UnitPrice    = request.UnitPrice,
            ReorderLevel = request.ReorderLevel
        };

        var result = await _productService.UpdateProductAsync(role, product);

        if (result.IsFailure)
        {
            var statusCode = result.ErrorCode switch
            {
                "NOT_FOUND"              => 404,
                "DUPLICATE_PRODUCT_CODE" => 409,
                "CONFLICT"               => 409,
                "FORBIDDEN"              => 403,
                _                        => 400
            };
            return StatusCode(statusCode, new ErrorResponse(new ErrorDetail(result.ErrorCode ?? "BAD_REQUEST", result.ErrorMessage ?? "Failed to update product.")));
        }

        return Ok(MapToResponse(result.Value!));
    }

    /// <summary>DELETE /products/{id} — Warehouse Manager only</summary>
    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        var role = GetUserRole();
        if (role != UserRoles.WarehouseManager)
        {
            return StatusCode(403, new ErrorResponse(new ErrorDetail("FORBIDDEN", "Only warehouse managers can delete products.")));
        }

        var result = await _productService.DeleteProductAsync(role, id);

        if (result.IsFailure)
        {
            var statusCode = result.ErrorCode == "NOT_FOUND" ? 404 : 400;
            return StatusCode(statusCode, new ErrorResponse(new ErrorDetail(result.ErrorCode ?? "BAD_REQUEST", result.ErrorMessage ?? "Failed to delete product.")));
        }

        return NoContent();
    }

    private string GetUserRole() =>
        User.FindFirst(ClaimTypes.Role)?.Value
        ?? throw new InvalidOperationException("Role claim is missing from authenticated token.");

    internal static ProductResponse MapToResponse(Product p) =>
        new(p.Id, p.ProductCode, p.Name, p.CategoryId, p.CategoryName, p.Quantity, p.UnitPrice, p.ReorderLevel, p.IsLowStock, p.CreatedAt, p.UpdatedAt);
}
