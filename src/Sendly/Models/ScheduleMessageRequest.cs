using System.Text.Json.Serialization;

namespace Sendly.Models;

/// <summary>
/// Request object for scheduling an SMS message.
/// </summary>
public class ScheduleMessageRequest
{
    /// <summary>
    /// Recipient phone number in E.164 format.
    /// </summary>
    [JsonPropertyName("to")]
    public string To { get; set; }

    /// <summary>
    /// Message content.
    /// </summary>
    [JsonPropertyName("text")]
    public string Text { get; set; }

    /// <summary>
    /// ISO 8601 datetime for delivery (must be at least 1 minute in the future).
    /// </summary>
    [JsonPropertyName("scheduled_at")]
    public string ScheduledAt { get; set; }

    /// <summary>
    /// Optional sender ID.
    /// </summary>
    [JsonPropertyName("from")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? From { get; set; }

    /// <summary>
    /// Creates a new schedule message request.
    /// </summary>
    /// <param name="to">Recipient phone number in E.164 format</param>
    /// <param name="text">Message content</param>
    /// <param name="scheduledAt">ISO 8601 datetime for delivery</param>
    /// <param name="from">Optional sender ID</param>
    public ScheduleMessageRequest(string to, string text, string scheduledAt, string? from = null)
    {
        To = to;
        Text = text;
        ScheduledAt = scheduledAt;
        From = from;
    }
}
