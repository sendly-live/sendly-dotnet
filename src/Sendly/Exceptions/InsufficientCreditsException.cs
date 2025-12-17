namespace Sendly.Exceptions;

/// <summary>
/// Thrown when the account has insufficient credits.
/// </summary>
public class InsufficientCreditsException : SendlyException
{
    /// <summary>
    /// Creates a new InsufficientCreditsException.
    /// </summary>
    public InsufficientCreditsException(string message = "Insufficient credits")
        : base(message, 402, "INSUFFICIENT_CREDITS")
    {
    }
}
