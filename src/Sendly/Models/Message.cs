using System.Text.Json;
using System.Text.Json.Serialization;

namespace Sendly.Models;

/// <summary>
/// Represents an SMS message.
/// </summary>
public class Message
{
    /// <summary>
    /// Message status constants.
    /// </summary>
    public static class Statuses
    {
        public const string Queued = "queued";
        public const string Sending = "sending";
        public const string Sent = "sent";
        public const string Delivered = "delivered";
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
    /// Delivery status.
    /// </summary>
    [JsonPropertyName("status")]
    public string Status { get; set; } = string.Empty;

    /// <summary>
    /// Credits consumed.
    /// </summary>
    [JsonPropertyName("credits_used")]
    public int CreditsUsed { get; set; }

    /// <summary>
    /// Creation timestamp.
    /// </summary>
    [JsonPropertyName("created_at")]
    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// Last update timestamp.
    /// </summary>
    [JsonPropertyName("updated_at")]
    public DateTime UpdatedAt { get; set; }

    /// <summary>
    /// Delivery timestamp (if delivered).
    /// </summary>
    [JsonPropertyName("delivered_at")]
    public DateTime? DeliveredAt { get; set; }

    /// <summary>
    /// Error code (if failed).
    /// </summary>
    [JsonPropertyName("error_code")]
    public string? ErrorCode { get; set; }

    /// <summary>
    /// Error message (if failed).
    /// </summary>
    [JsonPropertyName("error_message")]
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// Whether the message was delivered.
    /// </summary>
    public bool IsDelivered => Status == Statuses.Delivered;

    /// <summary>
    /// Whether the message failed.
    /// </summary>
    public bool IsFailed => Status == Statuses.Failed;

    /// <summary>
    /// Whether the message is pending.
    /// </summary>
    public bool IsPending => Status is Statuses.Queued or Statuses.Sending or Statuses.Sent;

    /// <summary>
    /// Creates a Message from a JSON element.
    /// </summary>
    internal static Message FromJson(JsonElement element, JsonSerializerOptions options)
    {
        return JsonSerializer.Deserialize<Message>(element.GetRawText(), options)
            ?? new Message();
    }
}
