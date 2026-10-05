using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StockPulse.API.DTOs;
using StockPulse.BLL.Interfaces;
using StockPulse.Domain.Entities;

namespace StockPulse.API.Controllers;

[ApiController]
[Route("categories")]
[Route("api/categories")]
[Authorize]
public sealed class CategoriesController : ControllerBase
{
    private readonly ICategoryService _categoryService;

    public CategoriesController(ICategoryService categoryService)
    {
        _categoryService = categoryService;
    }

    /// <summary>GET /categories</summary>
    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var result = await _categoryService.GetAllAsync();

        if (result.IsFailure)
        {
            return StatusCode(500, new ErrorResponse(new ErrorDetail(result.ErrorCode ?? "INTERNAL_ERROR", result.ErrorMessage ?? "Failed to retrieve categories.")));
        }

        var response = result.Value!.Select(c => new CategoryResponse(c.Id, c.Name));
        return Ok(response);
    }

    /// <summary>POST /categories — Warehouse Manager only</summary>
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateCategoryRequest request)
    {
        var role = GetUserRole();
        if (role != UserRoles.WarehouseManager)
        {
            return StatusCode(403, new ErrorResponse(new ErrorDetail("FORBIDDEN", "Only warehouse managers can add categories.")));
        }

        if (request is null || string.IsNullOrWhiteSpace(request.Name))
        {
            return BadRequest(new ErrorResponse(new ErrorDetail("VALIDATION_ERROR", "Category name is required.")));
        }

        var result = await _categoryService.AddAsync(role, request.Name);

        if (result.IsFailure)
        {
            var statusCode = result.ErrorCode switch
            {
                "CONFLICT"         => 409,
                "FORBIDDEN"        => 403,
                "VALIDATION_ERROR" => 400,
                _                  => 400
            };
            return StatusCode(statusCode, new ErrorResponse(new ErrorDetail(result.ErrorCode ?? "BAD_REQUEST", result.ErrorMessage ?? "Failed to create category.")));
        }

        return StatusCode(201, new CategoryResponse(result.Value!.Id, result.Value.Name));
    }

    private string GetUserRole() =>
        User.FindFirst(ClaimTypes.Role)?.Value
        ?? throw new InvalidOperationException("Role claim is missing from authenticated token.");
}
