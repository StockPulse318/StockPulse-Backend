namespace StockPulse.API.DTOs;

public sealed record LoginRequest(string Username, string Password);

public sealed record LoginResponse(string Token, string Username, string Role);

public sealed record RegisterUserRequest(string Username, string Password, string Role);

public sealed record UserResponse(string Username, string Role);
