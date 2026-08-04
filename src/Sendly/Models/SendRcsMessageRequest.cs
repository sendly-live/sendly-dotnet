using System.Text.Json.Serialization;

namespace Sendly.Models;

/// <summary>
/// Request to send an RCS message.
///
/// Provide exactly one of:
///
/// - <see cref="Text"/> — plain text, optionally with <see cref="Suggestions"/>
/// (tap-to-reply and open-URL chips)
///
/// - <see cref="Card"/> — a rich card with a title, description, optional
/// image, and optional chips
///
/// Delivery picks the leg per recipient: RCS when the recipient's device
/// supports it, otherwise an automatic SMS fallback (billed as SMS) for text
/// sends. Set <see cref="FallbackToSms"/> to <c>false</c> to get a 422
/// <c>rcs_not_supported_for_recipient</c> instead. Rich cards have no SMS form
/// and never fall back.
///
/// RCS sends require a live API key and an RCS agent registered on your
/// workspace (see <c>client.Rcs.Agents</c>).
/// </summary>
public class SendRcsMessageRequest
{
    /// <summary>
    /// Selects the RCS channel. Always "rcs".
    /// </summary>
    [JsonPropertyName("channel")]
    public string Channel { get; } = "rcs";

    /// <summary>
    /// Destination phone number in E.164 format (e.g., +15551234567).
    /// </summary>
    [JsonPropertyName("to")]
    public string To { get; set; }

    /// <summary>
    /// The RCS agent to send as. Optional when your workspace has exactly one
    /// agent; required (400 <c>rcs_agent_ambiguous</c>) when it has more.
    /// </summary>
    [JsonPropertyName("agentId")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? AgentId { get; set; }

    /// <summary>
    /// Plain message text. Provide exactly one of <see cref="Text"/> or
    /// <see cref="Card"/>. Text sends fall back to SMS for non-RCS recipients.
    /// </summary>
    [JsonPropertyName("text")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Text { get; set; }

    /// <summary>
    /// Rich card to send. Provide exactly one of <see cref="Text"/> or
    /// <see cref="Card"/>. Cards have no SMS form and never fall back.
    /// </summary>
    [JsonPropertyName("card")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public RcsCard? Card { get; set; }

    /// <summary>
    /// Suggestion chips shown under a text message. Only valid alongside
    /// <see cref="Text"/> — put card chips in <see cref="RcsCard.Suggestions"/>.
    /// Dropped when the send falls back to SMS (disclosed as
    /// <c>SuggestionsDropped</c> on the response).
    /// </summary>
    [JsonPropertyName("suggestions")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<RcsSuggestion>? Suggestions { get; set; }

    /// <summary>
    /// Whether a text send to a non-RCS recipient falls back to SMS (billed as
    /// SMS). Defaults to true when omitted.
    /// </summary>
    [JsonPropertyName("fallbackToSms")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public bool? FallbackToSms { get; set; }

    /// <summary>
    /// Custom metadata to attach to the message (max 4KB).
    /// </summary>
    [JsonPropertyName("metadata")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public Dictionary<string, object>? Metadata { get; set; }

    /// <summary>
    /// Creates a new RCS send request.
    /// </summary>
    public SendRcsMessageRequest(string to, string? text = null, RcsCard? card = null, string? agentId = null, List<RcsSuggestion>? suggestions = null, bool? fallbackToSms = null, Dictionary<string, object>? metadata = null)
    {
        To = to;
        Text = text;
        Card = card;
        AgentId = agentId;
        Suggestions = suggestions;
        FallbackToSms = fallbackToSms;
        Metadata = metadata;
    }
}

/// <summary>
/// A standalone RCS rich card — title, description, optional image, and
/// optional suggestion chips.
///
/// Rich cards have no SMS form: sending a card to a recipient whose device
/// doesn't support RCS fails with 422 <c>rcs_not_supported_for_recipient</c>
/// rather than falling back.
/// </summary>
public class RcsCard
{
    /// <summary>Card title. Required.</summary>
    [JsonPropertyName("title")]
    public string Title { get; set; } = string.Empty;

    /// <summary>Card description. Required.</summary>
    [JsonPropertyName("description")]
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// Image shown on the card. Must be a public JPEG, PNG, or GIF URL.
    /// </summary>
    [JsonPropertyName("mediaUrl")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? MediaUrl { get; set; }

    /// <summary>
    /// Card layout: <c>vertical</c> (default) or <c>horizontal</c>.
    /// </summary>
    [JsonPropertyName("orientation")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Orientation { get; set; }

    /// <summary>Suggestion chips shown under the card.</summary>
    [JsonPropertyName("suggestions")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<RcsSuggestion>? Suggestions { get; set; }
}

/// <summary>
/// A suggestion chip on an RCS message — exactly one of <see cref="Reply"/> or
/// <see cref="Action"/>.
///
/// - Reply — a tap-to-reply chip; the tap comes back as an inbound message
/// carrying your <c>postbackData</c>
///
/// - Action — an open-URL chip; tapping opens the URL and the tap is reported
/// with your <c>postbackData</c>
///
/// Chips have no SMS form: when a text send falls back to SMS they are dropped
/// (disclosed as <c>SuggestionsDropped</c> on the response).
/// </summary>
public class RcsSuggestion
{
    /// <summary>The tap-to-reply chip, when this is a reply suggestion.</summary>
    [JsonPropertyName("reply")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public RcsReplySuggestion? Reply { get; set; }

    /// <summary>The open-URL chip, when this is an action suggestion.</summary>
    [JsonPropertyName("action")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public RcsActionSuggestion? Action { get; set; }

    /// <summary>
    /// Creates a tap-to-reply chip.
    /// </summary>
    /// <param name="text">The chip label the recipient sees</param>
    /// <param name="postbackData">Opaque payload returned when the chip is tapped</param>
    public static RcsSuggestion CreateReply(string text, string postbackData) =>
        new() { Reply = new RcsReplySuggestion { Text = text, PostbackData = postbackData } };

    /// <summary>
    /// Creates an open-URL chip.
    /// </summary>
    /// <param name="text">The chip label the recipient sees</param>
    /// <param name="postbackData">Opaque payload reported when the chip is tapped</param>
    /// <param name="url">The URL the chip opens</param>
    public static RcsSuggestion CreateAction(string text, string postbackData, string url) =>
        new() { Action = new RcsActionSuggestion { Text = text, PostbackData = postbackData, Url = url } };
}

/// <summary>
/// A tap-to-reply suggestion chip.
/// </summary>
public class RcsReplySuggestion
{
    /// <summary>The chip label the recipient sees.</summary>
    [JsonPropertyName("text")]
    public string Text { get; set; } = string.Empty;

    /// <summary>Opaque payload returned when the chip is tapped.</summary>
    [JsonPropertyName("postbackData")]
    public string PostbackData { get; set; } = string.Empty;
}

/// <summary>
/// An open-URL suggestion chip.
/// </summary>
public class RcsActionSuggestion
{
    /// <summary>The chip label the recipient sees.</summary>
    [JsonPropertyName("text")]
    public string Text { get; set; } = string.Empty;

    /// <summary>Opaque payload reported when the chip is tapped.</summary>
    [JsonPropertyName("postbackData")]
    public string PostbackData { get; set; } = string.Empty;

    /// <summary>The URL the chip opens.</summary>
    [JsonPropertyName("url")]
    public string Url { get; set; } = string.Empty;
}
