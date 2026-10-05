using System.Security.Cryptography;
using Microsoft.AspNetCore.Cryptography.KeyDerivation;
using StockPulse.BLL.Interfaces;
using StockPulse.DAL.Interfaces;
using StockPulse.Domain.Entities;
using StockPulse.Domain.Exceptions;
using StockPulse.Domain.Results;

namespace StockPulse.BLL.Services;

// Stored hash format: "{iterations}.{base64(salt)}.{base64(hash)}"
// Self-describing so the iteration count can be increased later without breaking existing accounts.
public sealed class AuthService : IAuthService
{
    private const int Pbkdf2Iterations = 310_000;
    private const int SaltSizeBytes    = 16;
    private const int HashSizeBytes    = 32;

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

            if (!user.IsActive)
                return Result<User>.Failure("This account has been deactivated or suspended. Please contact your system administrator.");

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
        string role,
        string? fullName = null,
        string? assignedBranch = null)
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
                Username       = newUsername.Trim(),
                PasswordHash   = HashPassword(password),
                Role           = role,
                FullName       = fullName?.Trim() ?? string.Empty,
                AssignedBranch = string.IsNullOrWhiteSpace(assignedBranch) ? "All Branches" : assignedBranch.Trim(),
                IsActive       = true,
                CreatedAt      = DateTime.UtcNow.ToString("o")
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

    public async Task<Result> UpdateUserAsync(
        string actorUsername,
        string targetUsername,
        string? fullName,
        string? role,
        string? assignedBranch,
        bool? isActive)
    {
        try
        {
            var actor = await ResolveActorAsync(actorUsername);
            EnforceManagerRole(actor, "update user accounts");

            var target = await _userRepository.GetByUsernameAsync(targetUsername);
            if (target is null)
                return Result.Failure($"User '{targetUsername}' does not exist.");

            // Self-modification safeguard: Cannot deactivate yourself
            if (actor.Username.Equals(targetUsername, StringComparison.OrdinalIgnoreCase) && isActive == false)
                return Result.Failure("You cannot deactivate your own account.");

            if (role != null && !UserRoles.IsValid(role))
                return Result.Failure($"Invalid role. Accepted values: {string.Join(", ", UserRoles.All)}");

            var updatedUser = new User
            {
                Username       = target.Username,
                PasswordHash   = target.PasswordHash,
                Role           = role ?? target.Role,
                FullName       = fullName ?? target.FullName,
                AssignedBranch = assignedBranch ?? target.AssignedBranch,
                IsActive       = isActive ?? target.IsActive,
                CreatedAt      = target.CreatedAt
            };

            await _userRepository.UpdateAsync(updatedUser);
            return Result.Success();
        }
        catch (UnauthorizedActionException ex)
        {
            return Result.Failure(ex.Message, ex);
        }
        catch (Exception ex)
        {
            return Result.Failure("Failed to update user.", ex);
        }
    }

    public async Task<Result> ResetPasswordAsync(
        string actorUsername,
        string targetUsername,
        string newPassword)
    {
        try
        {
            var actor = await ResolveActorAsync(actorUsername);
            EnforceManagerRole(actor, "reset user passwords");

            if (string.IsNullOrWhiteSpace(newPassword) || newPassword.Length < 8)
                return Result.Failure("New password must be at least 8 characters long.");

            var target = await _userRepository.GetByUsernameAsync(targetUsername);
            if (target is null)
                return Result.Failure($"User '{targetUsername}' does not exist.");

            var newHash = HashPassword(newPassword);
            await _userRepository.UpdatePasswordAsync(targetUsername, newHash);
            return Result.Success();
        }
        catch (UnauthorizedActionException ex)
        {
            return Result.Failure(ex.Message, ex);
        }
        catch (Exception ex)
        {
            return Result.Failure("Failed to reset password.", ex);
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

    private async Task<User> ResolveActorAsync(string actorUsername)
    {
        return await _userRepository.GetByUsernameAsync(actorUsername)
            ?? throw new InvalidOperationException($"Actor '{actorUsername}' not found.");
    }

    private static void EnforceManagerRole(User actor, string attemptedAction)
    {
        if (!actor.IsWarehouseManager)
            throw new UnauthorizedActionException(actor.Username, actor.Role, attemptedAction);
    }

    private static string HashPassword(string password)
    {
        var salt = RandomNumberGenerator.GetBytes(SaltSizeBytes);
        var hash = KeyDerivation.Pbkdf2(
            password:         password,
            salt:             salt,
            prf:              KeyDerivationPrf.HMACSHA256,
            iterationCount:   Pbkdf2Iterations,
            numBytesRequested: HashSizeBytes);

        return $"{Pbkdf2Iterations}.{Convert.ToBase64String(salt)}.{Convert.ToBase64String(hash)}";
    }

    // FixedTimeEquals prevents timing attacks — a naive string compare would return early
    // on the first mismatched byte, leaking information about how close a guess was.
    private static bool VerifyPassword(string password, string storedHash)
    {
        var parts = storedHash.Split('.');
        if (parts.Length != 3 || !int.TryParse(parts[0], out var iterations))
            return false;

        byte[] salt, expectedHash;
        try
        {
            salt         = Convert.FromBase64String(parts[1]);
            expectedHash = Convert.FromBase64String(parts[2]);
        }
        catch (FormatException)
        {
            return false;
        }

        var actualHash = KeyDerivation.Pbkdf2(
            password:         password,
            salt:             salt,
            prf:              KeyDerivationPrf.HMACSHA256,
            iterationCount:   iterations,
            numBytesRequested: expectedHash.Length);

        return CryptographicOperations.FixedTimeEquals(actualHash, expectedHash);
    }
}
