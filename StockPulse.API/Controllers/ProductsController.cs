using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StockPulse.API.DTOs;
using StockPulse.BLL.Interfaces;
using StockPulse.Domain.Entities;

namespace StockPulse.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public sealed class ProductsController : ControllerBase
{
    private readonly IProductService _productService;

    public ProductsController(IProductService productService)
    {
        _productService = productService;
    }

    /// <summary>GET api/products</summary>
    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var result = await _productService.GetAllAsync();

        if (result.IsFailure)
            return StatusCode(500, new { error = result.ErrorMessage });

        return Ok(result.Value!.Select(MapToResponse));
    }

    /// <summary>GET api/products/low-stock</summary>
    [HttpGet("low-stock")]
    public async Task<IActionResult> GetLowStock()
    {
        var result = await _productService.GetLowStockAsync();

        if (result.IsFailure)
            return StatusCode(500, new { error = result.ErrorMessage });

        return Ok(result.Value!.Select(MapToResponse));
    }

    /// <summary>GET api/products/search?name={partial}</summary>
    [HttpGet("search")]
    public async Task<IActionResult> Search([FromQuery] string name)
    {
        var result = await _productService.SearchByNameAsync(name);

        if (result.IsFailure)
            return BadRequest(new { error = result.ErrorMessage });

        return Ok(result.Value!.Select(MapToResponse));
    }

    /// <summary>GET api/products/{id}</summary>
    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetById(int id)
    {
        var result = await _productService.GetByIdAsync(id);

        if (result.IsFailure)
            return NotFound(new { error = result.ErrorMessage });

        return Ok(MapToResponse(result.Value!));
    }

    /// <summary>GET api/products/branches</summary>
    [HttpGet("branches")]
    public async Task<IActionResult> GetBranches()
    {
        var result = await _productService.GetBranchesAsync();

        if (result.IsFailure)
            return StatusCode(500, new { error = result.ErrorMessage });

        return Ok(result.Value);
    }

    /// <summary>GET api/products/categories</summary>
    [HttpGet("categories")]
    public async Task<IActionResult> GetCategories()
    {
        var result = await _productService.GetCategoriesAsync();

        if (result.IsFailure)
            return StatusCode(500, new { error = result.ErrorMessage });

        return Ok(result.Value);
    }

    /// <summary>POST api/products — Warehouse Manager only</summary>
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateProductRequest request)
    {
        var actorUsername = GetActorUsername();

        var branch = !string.IsNullOrWhiteSpace(request.Branch)
            ? request.Branch.Trim()
            : request.Category.Contains(" - ")
                ? request.Category.Split(" - ", 2)[0].Trim()
                : "Main Warehouse";

        var category = request.Category.Contains(" - ") && string.IsNullOrWhiteSpace(request.Branch)
            ? request.Category.Split(" - ", 2)[1].Trim()
            : request.Category.Trim();

        var product = new Product
        {
            ProductName  = request.ProductName,
            Branch       = branch,
            Category     = category,
            Quantity     = request.Quantity,
            UnitPrice    = request.UnitPrice,
            ReorderLevel = request.ReorderLevel
        };

        var result = await _productService.AddProductAsync(actorUsername, product);

        if (result.IsFailure)
            return BadRequest(new { error = result.ErrorMessage });

        return CreatedAtAction(nameof(GetById), new { id = result.Value }, new { productId = result.Value });
    }

    /// <summary>PUT api/products/{id} — Warehouse Manager only</summary>
    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, [FromBody] UpdateProductRequest request)
    {
        var actorUsername = GetActorUsername();

        var branch = !string.IsNullOrWhiteSpace(request.Branch)
            ? request.Branch.Trim()
            : request.Category.Contains(" - ")
                ? request.Category.Split(" - ", 2)[0].Trim()
                : "Main Warehouse";

        var category = request.Category.Contains(" - ") && string.IsNullOrWhiteSpace(request.Branch)
            ? request.Category.Split(" - ", 2)[1].Trim()
            : request.Category.Trim();

        // Quantity is intentionally excluded from UpdateProductRequest —
        // stock levels are only modified through the /stock endpoints.
        var product = new Product
        {
            ProductID    = id,
            ProductName  = request.ProductName,
            Branch       = branch,
            Category     = category,
            UnitPrice    = request.UnitPrice,
            ReorderLevel = request.ReorderLevel
        };

        var result = await _productService.UpdateProductAsync(actorUsername, product);

        if (result.IsFailure)
            return BadRequest(new { error = result.ErrorMessage });

        return NoContent();
    }

    /// <summary>DELETE api/products/{id} — Warehouse Manager only</summary>
    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        var actorUsername = GetActorUsername();
        var result = await _productService.DeleteProductAsync(actorUsername, id);

        if (result.IsFailure)
            return BadRequest(new { error = result.ErrorMessage });

        return NoContent();
    }

    private string GetActorUsername() =>
        User.Identity?.Name
            ?? throw new InvalidOperationException("Authenticated user identity is missing from token.");

    private static ProductResponse MapToResponse(Product p) =>
        new(p.ProductID, p.ProductName, p.Category, p.Quantity, p.UnitPrice, p.ReorderLevel, p.IsLowStock, p.Branch);
}
