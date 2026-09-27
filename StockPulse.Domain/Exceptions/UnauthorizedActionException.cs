namespace StockPulse.Domain.Exceptions;

/// <summary>
/// Raised when a user attempts an operation their role does not permit.
/// Distinct from System.UnauthorizedAccessException so the BLL can be
/// explicit about RBAC violations vs. OS-level permission errors.
/// </summary>
public sealed class UnauthorizedActionException : Exception
{
    public string Username { get; }
    public string Role { get; }
    public string AttemptedAction { get; }

    public UnauthorizedActionException(string username, string role, string attemptedAction)
        : base($"User '{username}' (role: '{role}') is not authorized to perform: {attemptedAction}.")
    {
        Username = username;
        Role = role;
        AttemptedAction = attemptedAction;
    }
}
