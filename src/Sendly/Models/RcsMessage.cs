using System.Text.Json;
using System.Text.Json.Serialization;

namespace Sendly.Models;

/// <summary>
/// A sent RCS message — or its SMS fallback.
///
/// Check <see cref="Channel"/> (or <see cref="FellBackToSms"/>) to see which
/// leg delivered: <see cref="Channel"/> is <c>rcs</c> on a native RCS send and
/// <c>sms</c> when the recipient's device doesn't support RCS and the message
/// fell back to SMS (billed as SMS). <see cref="Rcs"/> carries the
/// leg-specific details either way.
/// </summary>
public class RcsMessage
{
    /// <summary>
    /// Unique message identifier.
    /// </summary>
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// The channel that delivered: <c>rcs</c>, or <c>sms</c> when the send fell
    /// back.
    /// </summary>
    [JsonPropertyName("channel")]
    public string Channel { get; set; } = string.Empty;

    /// <summary>
    /// <c>"sms"</c> when this send fell back to SMS; null on a native RCS send.
    /// </summary>
    [JsonPropertyName("fellBackTo")]
    public string? FellBackTo { get; set; }

    /// <summary>
    /// <c>rcs</c>, or <c>sms</c> when the send fell back.
    /// </summary>
    [JsonPropertyName("message_format")]
    public string MessageFormat { get; set; } = string.Empty;

    /// <summary>
    /// Destination phone number.
    /// </summary>
    [JsonPropertyName("to")]
    public string To { get; set; } = string.Empty;

    /// <summary>
    /// The sender the recipient sees: the RCS agent name, or the SMS sending
    /// number on a fallback.
    /// </summary>
    [JsonPropertyName("from")]
    public string From { get; set; } = string.Empty;

    /// <summary>
    /// Body text for text sends; null for card sends.
    /// </summary>
    [JsonPropertyName("text")]
    public string? Text { get; set; }

    /// <summary>
    /// Current delivery status.
    /// </summary>
    [JsonPropertyName("status")]
    public string Status { get; set; } = string.Empty;

    /// <summary>
    /// Always 1 for native RCS; the SMS segment count on a fallback.
    /// </summary>
    [JsonPropertyName("segments")]
    public int Segments { get; set; } = 1;

    /// <summary>
    /// Credits charged for this message — RCS pricing natively, SMS pricing on
    /// a fallback.
    /// </summary>
    [JsonPropertyName("creditsUsed")]
    public int CreditsUsed { get; set; }

    /// <summary>
    /// RCS-specific details for whichever leg delivered.
    /// </summary>
    [JsonPropertyName("rcs")]
    public RcsMessageDetails Rcs { get; set; } = new();

    /// <summary>
    /// When the message was created (ISO 8601).
    /// </summary>
    [JsonPropertyName("createdAt")]
    public string CreatedAt { get; set; } = string.Empty;

    /// <summary>
    /// Custom metadata attached to the message.
    /// </summary>
    [JsonPropertyName("metadata")]
    public Dictionary<string, object>? Metadata { get; set; }

    /// <summary>
    /// True when the recipient couldn't receive RCS and the message was
    /// delivered (and billed) as SMS instead.
    /// </summary>
    [JsonIgnore]
    public bool FellBackToSms => FellBackTo == "sms";

    /// <summary>
    /// Creates an RcsMessage from a JSON element.
    /// </summary>
    internal static RcsMessage FromJson(JsonElement element, JsonSerializerOptions options)
    {
        return JsonSerializer.Deserialize<RcsMessage>(element.GetRawText(), options)
            ?? new RcsMessage();
    }
}

/// <summary>
/// RCS-specific details on a sent message.
/// </summary>
public class RcsMessageDetails
{
    /// <summary>
    /// What was sent natively: <c>text</c> or <c>card</c>; null on a fallback.
    /// </summary>
    [JsonPropertyName("kind")]
    public string? Kind { get; set; }

    /// <summary>
    /// The agent the message was sent as.
    /// </summary>
    [JsonPropertyName("agentId")]
    public string AgentId { get; set; } = string.Empty;

    /// <summary>
    /// The agent's display name (native RCS sends only); null on a fallback.
    /// </summary>
    [JsonPropertyName("agentName")]
    public string? AgentName { get; set; }

    /// <summary>
    /// <c>"rcs"</c> on a fallback — the channel that was asked for; null on a
    /// native send.
    /// </summary>
    [JsonPropertyName("requestedChannel")]
    public string? RequestedChannel { get; set; }

    /// <summary>
    /// True when suggestion chips were dropped because the send fell back to
    /// SMS (chips have no SMS form); null otherwise.
    /// </summary>
    [JsonPropertyName("suggestionsDropped")]
    public bool? SuggestionsDropped { get; set; }
}
