namespace Sendly.Exceptions;

/// <summary>
/// Thrown when a network error occurs.
/// </summary>
public class NetworkException : SendlyException
{
    /// <summary>
    /// Creates a new NetworkException.
    /// </summary>
    public NetworkException(string message = "Network error occurred", Exception? innerException = null)
        : base(message, 0, "NETWORK_ERROR", innerException)
    {
    }
}
