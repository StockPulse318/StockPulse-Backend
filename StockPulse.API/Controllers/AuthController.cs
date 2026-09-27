using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StockPulse.API.DTOs;
using StockPulse.API.Services;
using StockPulse.BLL.Interfaces;

namespace StockPulse.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public sealed class AuthController : ControllerBase
{
    private readonly IAuthService _authService;
    private readonly TokenService _tokenService;

    public AuthController(IAuthService authService, TokenService tokenService)
    {
        _authService = authService;
        _tokenService = tokenService;
    }

    /// <summary>POST api/auth/login</summary>
    [HttpPost("login")]
    [AllowAnonymous]
    public async Task<IActionResult> Login([FromBody] LoginRequest request)
    {
        var result = await _authService.LoginAsync(request.Username, request.Password);

        if (result.IsFailure)
            return Unauthorized(new { error = result.ErrorMessage });

        var token = _tokenService.GenerateToken(result.Value!);

        return Ok(new LoginResponse(token, result.Value!.Username, result.Value.Role));
    }

    /// <summary>POST api/auth/users — Warehouse Manager only</summary>
    [HttpPost("users")]
    [Authorize]
    public async Task<IActionResult> RegisterUser([FromBody] RegisterUserRequest request)
    {
        var actorUsername = GetActorUsername();
        var result = await _authService.RegisterUserAsync(
            actorUsername, request.Username, request.Password, request.Role);

        if (result.IsFailure)
            return BadRequest(new { error = result.ErrorMessage });

        return StatusCode(201);
    }

    /// <summary>GET api/auth/users — Warehouse Manager only</summary>
    [HttpGet("users")]
    [Authorize]
    public async Task<IActionResult> GetAllUsers()
    {
        var actorUsername = GetActorUsername();
        var result = await _authService.GetAllUsersAsync(actorUsername);

        if (result.IsFailure)
            return Forbid();

        var response = result.Value!.Select(u => new UserResponse(u.Username, u.Role));
        return Ok(response);
    }

    /// <summary>DELETE api/auth/users/{username} — Warehouse Manager only</summary>
    [HttpDelete("users/{username}")]
    [Authorize]
    public async Task<IActionResult> DeleteUser(string username)
    {
        var actorUsername = GetActorUsername();
        var result = await _authService.DeleteUserAsync(actorUsername, username);

        if (result.IsFailure)
            return BadRequest(new { error = result.ErrorMessage });

        return NoContent();
    }

    private string GetActorUsername() =>
        User.Identity?.Name
            ?? throw new InvalidOperationException("Authenticated user identity is missing from token.");
}
