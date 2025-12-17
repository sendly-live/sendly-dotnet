namespace Sendly.Exceptions;

/// <summary>
/// Thrown when the API key is invalid or missing.
/// </summary>
public class AuthenticationException : SendlyException
{
    /// <summary>
    /// Creates a new AuthenticationException.
    /// </summary>
    public AuthenticationException(string message = "Invalid or missing API key")
        : base(message, 401, "AUTHENTICATION_ERROR")
    {
    }
}
