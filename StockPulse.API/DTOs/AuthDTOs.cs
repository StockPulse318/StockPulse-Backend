namespace StockPulse.API.DTOs;

public sealed record LoginRequest(string Username, string Password);

public sealed record LoginResponse(
    string Token,
    string Username,
    string Role,
    string FullName);
