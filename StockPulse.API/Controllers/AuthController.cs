using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using StockPulse.API.DTOs;
using StockPulse.API.Services;
using StockPulse.BLL.Interfaces;

namespace StockPulse.API.Controllers;

[ApiController]
[Route("auth")]
[Route("api/auth")]
public sealed class AuthController : ControllerBase
{
    private readonly IAuthService _authService;
    private readonly TokenService _tokenService;

    public AuthController(IAuthService authService, TokenService tokenService)
    {
        _authService = authService;
        _tokenService = tokenService;
    }

    /// <summary>POST /auth/login</summary>
    [HttpPost("login")]
    [AllowAnonymous]
    [EnableRateLimiting("login-policy")]
    public async Task<IActionResult> Login([FromBody] LoginRequest request)
    {
        if (request is null || string.IsNullOrWhiteSpace(request.Username) || string.IsNullOrWhiteSpace(request.Password))
        {
            return BadRequest(new ErrorResponse(new ErrorDetail("VALIDATION_ERROR", "Username and password are required.")));
        }

        var result = await _authService.LoginAsync(request.Username, request.Password);

        if (result.IsFailure)
        {
            var statusCode = result.ErrorCode switch
            {
                "ACCOUNT_DEACTIVATED" => 403,
                "VALIDATION_ERROR"    => 400,
                _                     => 401
            };

            return StatusCode(statusCode, new ErrorResponse(new ErrorDetail(result.ErrorCode ?? "UNAUTHORIZED", result.ErrorMessage ?? "Login failed.")));
        }

        var token = _tokenService.GenerateToken(result.Value!);

        return Ok(new LoginResponse(
            token,
            result.Value!.Username,
            result.Value.Role,
            result.Value.FullName));
    }
}
