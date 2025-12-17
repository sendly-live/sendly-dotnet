namespace Sendly.Exceptions;

/// <summary>
/// Thrown when the requested resource is not found.
/// </summary>
public class NotFoundException : SendlyException
{
    /// <summary>
    /// Creates a new NotFoundException.
    /// </summary>
    public NotFoundException(string message = "Resource not found")
        : base(message, 404, "NOT_FOUND")
    {
    }
}
