using StockPulse.BLL.Interfaces;
using StockPulse.DAL.Interfaces;
using StockPulse.Domain.Entities;
using StockPulse.Domain.Results;

namespace StockPulse.BLL.Services;

public sealed class AuthService : IAuthService
{
    private readonly IUserRepository _userRepository;

    public AuthService(IUserRepository userRepository)
    {
        _userRepository = userRepository;
    }

    public async Task<Result<User>> LoginAsync(string username, string password)
    {
        if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
            return Result<User>.Failure("Username and password are required.", "VALIDATION_ERROR");

        try
        {
            var user = await _userRepository.GetByUsernameAsync(username.Trim());

            if (user is null || !VerifyPassword(password, user.PasswordHash))
                return Result<User>.Failure("Invalid username or password.", "INVALID_CREDENTIALS");

            if (!user.IsActive)
                return Result<User>.Failure("This account has been deactivated.", "ACCOUNT_DEACTIVATED");

            return Result<User>.Success(user);
        }
        catch (Exception ex)
        {
            return Result<User>.Failure("Login failed due to an unexpected error.", "INTERNAL_ERROR", ex);
        }
    }

    private static bool VerifyPassword(string password, string storedHash)
    {
        if (string.IsNullOrWhiteSpace(storedHash))
            return false;

        try
        {
            if (storedHash.StartsWith("$2a$") || storedHash.StartsWith("$2b$") || storedHash.StartsWith("$2y$"))
            {
                return BCrypt.Net.BCrypt.Verify(password, storedHash);
            }

            // Fallback for legacy PBKDF2 hashes ({iterations}.{salt}.{hash})
            if (storedHash.Contains('.'))
            {
                var parts = storedHash.Split('.');
                if (parts.Length == 3 && int.TryParse(parts[0], out var iterations))
                {
                    var salt = Convert.FromBase64String(parts[1]);
                    var expectedHash = Convert.FromBase64String(parts[2]);
                    var actualHash = Microsoft.AspNetCore.Cryptography.KeyDerivation.KeyDerivation.Pbkdf2(
                        password: password,
                        salt: salt,
                        prf: Microsoft.AspNetCore.Cryptography.KeyDerivation.KeyDerivationPrf.HMACSHA256,
                        iterationCount: iterations,
                        numBytesRequested: expectedHash.Length);
                    return System.Security.Cryptography.CryptographicOperations.FixedTimeEquals(actualHash, expectedHash);
                }
            }

            return false;
        }
        catch
        {
            return false;
        }
    }
}
