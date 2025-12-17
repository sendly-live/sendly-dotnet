namespace Sendly.Exceptions;

/// <summary>
/// Base exception for all Sendly errors.
/// </summary>
public class SendlyException : Exception
{
    /// <summary>
    /// HTTP status code.
    /// </summary>
    public int StatusCode { get; }

    /// <summary>
    /// Error code.
    /// </summary>
    public string? ErrorCode { get; }

    /// <summary>
    /// Creates a new SendlyException.
    /// </summary>
    public SendlyException(string message, int statusCode = 0, string? errorCode = null, Exception? innerException = null)
        : base(message, innerException)
    {
        StatusCode = statusCode;
        ErrorCode = errorCode;
    }
}
