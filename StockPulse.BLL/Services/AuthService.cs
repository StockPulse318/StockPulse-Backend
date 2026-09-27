using System.Security.Cryptography;
using Microsoft.AspNetCore.Cryptography.KeyDerivation;
using StockPulse.BLL.Interfaces;
using StockPulse.DAL.Interfaces;
using StockPulse.Domain.Entities;
using StockPulse.Domain.Exceptions;
using StockPulse.Domain.Results;

namespace StockPulse.BLL.Services;

/// <summary>
/// Handles credential verification, user provisioning, and RBAC guard for user management.
///
/// Password storage strategy:
///   PBKDF2-HMAC-SHA256, 128-bit salt, 256-bit subkey, 310,000 iterations.
///   Iteration count follows OWASP 2023 recommendations for PBKDF2-SHA256.
///   Salt is generated fresh per password so two users with the same password
///   produce different hashes, defeating rainbow table attacks.
///   Stored format: "iterations.base64(salt).base64(hash)" — self-describing so
///   the iteration count can be increased in future without breaking existing logins.
/// </summary>
public sealed class AuthService : IAuthService
{
    private const int Pbkdf2Iterations = 310_000;
    private const int SaltSizeBytes = 16;
    private const int HashSizeBytes = 32;

    private readonly IUserRepository _userRepository;

    public AuthService(IUserRepository userRepository)
    {
        _userRepository = userRepository;
    }

    public async Task<Result<User>> LoginAsync(string username, string password)
    {
        if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
            return Result<User>.Failure("Username and password are required.");

        try
        {
            var user = await _userRepository.GetByUsernameAsync(username.Trim());

            if (user is null || !VerifyPassword(password, user.PasswordHash))
                return Result<User>.Failure("Invalid username or password.");

            return Result<User>.Success(user);
        }
        catch (Exception ex)
        {
            return Result<User>.Failure("Login failed due to an unexpected error.", ex);
        }
    }

    public async Task<Result> RegisterUserAsync(
        string actorUsername,
        string newUsername,
        string password,
        string role)
    {
        try
        {
            var actor = await ResolveActorAsync(actorUsername);
            EnforceManagerRole(actor, "register new users");

            if (string.IsNullOrWhiteSpace(newUsername))
                return Result.Failure("Username cannot be empty.");

            if (string.IsNullOrWhiteSpace(password) || password.Length < 8)
                return Result.Failure("Password must be at least 8 characters.");

            if (!UserRoles.IsValid(role))
                return Result.Failure($"Invalid role. Accepted values: {string.Join(", ", UserRoles.All)}");

            var existing = await _userRepository.GetByUsernameAsync(newUsername.Trim());
            if (existing is not null)
                return Result.Failure($"Username '{newUsername}' is already taken.");

            var newUser = new User
            {
                Username = newUsername.Trim(),
                PasswordHash = HashPassword(password),
                Role = role
            };

            await _userRepository.AddAsync(newUser);
            return Result.Success();
        }
        catch (UnauthorizedActionException ex)
        {
            return Result.Failure(ex.Message, ex);
        }
        catch (Exception ex)
        {
            return Result.Failure("Failed to register user due to an unexpected error.", ex);
        }
    }

    public async Task<Result> DeleteUserAsync(string actorUsername, string targetUsername)
    {
        try
        {
            var actor = await ResolveActorAsync(actorUsername);
            EnforceManagerRole(actor, "delete users");

            if (actor.Username.Equals(targetUsername, StringComparison.OrdinalIgnoreCase))
                return Result.Failure("A user cannot delete their own account.");

            await _userRepository.DeleteAsync(targetUsername);
            return Result.Success();
        }
        catch (UnauthorizedActionException ex)
        {
            return Result.Failure(ex.Message, ex);
        }
        catch (Exception ex)
        {
            return Result.Failure("Failed to delete user due to an unexpected error.", ex);
        }
    }

    public async Task<Result<IEnumerable<User>>> GetAllUsersAsync(string actorUsername)
    {
        try
        {
            var actor = await ResolveActorAsync(actorUsername);
            EnforceManagerRole(actor, "view all users");

            var users = await _userRepository.GetAllAsync();
            return Result<IEnumerable<User>>.Success(users);
        }
        catch (UnauthorizedActionException ex)
        {
            return Result<IEnumerable<User>>.Failure(ex.Message, ex);
        }
        catch (Exception ex)
        {
            return Result<IEnumerable<User>>.Failure("Failed to retrieve users.", ex);
        }
    }

    // -------------------------------------------------------------------------
    // Private helpers
    // -------------------------------------------------------------------------

    private async Task<User> ResolveActorAsync(string actorUsername)
    {
        var actor = await _userRepository.GetByUsernameAsync(actorUsername)
            ?? throw new InvalidOperationException($"Actor '{actorUsername}' not found in the system.");
        return actor;
    }

    private static void EnforceManagerRole(User actor, string attemptedAction)
    {
        if (!actor.IsWarehouseManager)
            throw new UnauthorizedActionException(actor.Username, actor.Role, attemptedAction);
    }

    /// <summary>
    /// Produces a self-describing hash string containing the iteration count,
    /// salt, and derived key so that stored hashes remain verifiable even if
    /// the iteration count is tuned upwards in a future release.
    /// </summary>
    private static string HashPassword(string password)
    {
        var salt = RandomNumberGenerator.GetBytes(SaltSizeBytes);

        var hash = KeyDerivation.Pbkdf2(
            password: password,
            salt: salt,
            prf: KeyDerivationPrf.HMACSHA256,
            iterationCount: Pbkdf2Iterations,
            numBytesRequested: HashSizeBytes);

        return $"{Pbkdf2Iterations}.{Convert.ToBase64String(salt)}.{Convert.ToBase64String(hash)}";
    }

    /// <summary>
    /// Parses the stored hash descriptor and re-derives the key using the same
    /// parameters. CryptographicOperations.FixedTimeEquals is used to compare
    /// the byte arrays in constant time — preventing timing-based side-channel attacks
    /// where a naive string comparison might return early on first mismatch.
    /// </summary>
    private static bool VerifyPassword(string password, string storedHash)
    {
        var parts = storedHash.Split('.');
        if (parts.Length != 3)
            return false;

        if (!int.TryParse(parts[0], out var iterations))
            return false;

        byte[] salt;
        byte[] expectedHash;

        try
        {
            salt = Convert.FromBase64String(parts[1]);
            expectedHash = Convert.FromBase64String(parts[2]);
        }
        catch (FormatException)
        {
            return false;
        }

        var actualHash = KeyDerivation.Pbkdf2(
            password: password,
            salt: salt,
            prf: KeyDerivationPrf.HMACSHA256,
            iterationCount: iterations,
            numBytesRequested: expectedHash.Length);

        return CryptographicOperations.FixedTimeEquals(actualHash, expectedHash);
    }
}
