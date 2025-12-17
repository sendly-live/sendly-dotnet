namespace Sendly.Exceptions;

/// <summary>
/// Thrown when the rate limit is exceeded.
/// </summary>
public class RateLimitException : SendlyException
{
    /// <summary>
    /// Time to wait before retrying.
    /// </summary>
    public TimeSpan? RetryAfter { get; }

    /// <summary>
    /// Creates a new RateLimitException.
    /// </summary>
    public RateLimitException(string message = "Rate limit exceeded", TimeSpan? retryAfter = null)
        : base(message, 429, "RATE_LIMIT_EXCEEDED")
    {
        RetryAfter = retryAfter;
    }
}
