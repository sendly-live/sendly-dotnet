namespace Sendly.Models;

/// <summary>
/// Options for listing scheduled messages.
/// </summary>
public class ListScheduledMessagesOptions
{
    /// <summary>
    /// Maximum number of results to return.
    /// </summary>
    public int? Limit { get; set; }

    /// <summary>
    /// Number of results to skip.
    /// </summary>
    public int? Offset { get; set; }

    /// <summary>
    /// Filter by status.
    /// </summary>
    public string? Status { get; set; }

    /// <summary>
    /// Converts options to query parameters.
    /// </summary>
    internal Dictionary<string, string> ToQueryParams()
    {
        var parameters = new Dictionary<string, string>();

        if (Limit.HasValue)
            parameters["limit"] = Limit.Value.ToString();

        if (Offset.HasValue)
            parameters["offset"] = Offset.Value.ToString();

        if (!string.IsNullOrEmpty(Status))
            parameters["status"] = Status;

        return parameters;
    }
}
