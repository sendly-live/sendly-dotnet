using System.Text.Json;
using System.Text.Json.Serialization;

namespace Sendly.Models;

/// <summary>
/// Represents a scheduled SMS message.
/// </summary>
public class ScheduledMessage
{
    /// <summary>
    /// Scheduled message status constants.
    /// </summary>
    public static class Statuses
    {
        public const string Scheduled = "scheduled";
        public const string Sent = "sent";
        public const string Cancelled = "cancelled";
        public const string Failed = "failed";
    }

    /// <summary>
    /// Unique message identifier.
    /// </summary>
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// Recipient phone number in E.164 format.
    /// </summary>
    [JsonPropertyName("to")]
    public string To { get; set; } = string.Empty;

    /// <summary>
    /// Message content.
    /// </summary>
    [JsonPropertyName("text")]
    public string Text { get; set; } = string.Empty;

    /// <summary>
    /// Sender ID (optional).
    /// </summary>
    [JsonPropertyName("from")]
    public string? From { get; set; }

    /// <summary>
    /// Current status.
    /// </summary>
    [JsonPropertyName("status")]
    public string Status { get; set; } = string.Empty;

    /// <summary>
    /// Scheduled delivery time.
    /// </summary>
    [JsonPropertyName("scheduled_at")]
    public DateTime ScheduledAt { get; set; }

    /// <summary>
    /// Credits reserved for this message.
    /// </summary>
    [JsonPropertyName("credits_reserved")]
    public int CreditsReserved { get; set; }

    /// <summary>
    /// Creation timestamp.
    /// </summary>
    [JsonPropertyName("created_at")]
    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// Cancellation timestamp (if cancelled).
    /// </summary>
    [JsonPropertyName("cancelled_at")]
    public DateTime? CancelledAt { get; set; }

    /// <summary>
    /// Sent timestamp (if sent).
    /// </summary>
    [JsonPropertyName("sent_at")]
    public DateTime? SentAt { get; set; }

    /// <summary>
    /// Error message (if failed).
    /// </summary>
    [JsonPropertyName("error")]
    public string? Error { get; set; }

    /// <summary>
    /// Whether the message is still scheduled (pending delivery).
    /// </summary>
    public bool IsScheduled => Status == Statuses.Scheduled;

    /// <summary>
    /// Whether the message was sent.
    /// </summary>
    public bool IsSent => Status == Statuses.Sent;

    /// <summary>
    /// Whether the message was cancelled.
    /// </summary>
    public bool IsCancelled => Status == Statuses.Cancelled;

    /// <summary>
    /// Whether the message failed.
    /// </summary>
    public bool IsFailed => Status == Statuses.Failed;

    /// <summary>
    /// Creates a ScheduledMessage from a JSON element.
    /// </summary>
    internal static ScheduledMessage FromJson(JsonElement element, JsonSerializerOptions options)
    {
        return JsonSerializer.Deserialize<ScheduledMessage>(element.GetRawText(), options)
            ?? new ScheduledMessage();
    }
}
