using System.Text.Json;
using System.Text.Json.Serialization;

namespace Sendly.Models;

/// <summary>
/// Request to send a group MMS to 2-8 recipients (US/Canada only).
/// </summary>
public class SendGroupMessageRequest
{
    /// <summary>
    /// 2-8 recipient phone numbers in E.164 format. US and Canada only, and each
    /// must be an MMS-capable mobile.
    /// </summary>
    [JsonPropertyName("to")]
    public List<string> To { get; set; } = new();

    /// <summary>
    /// Message body. Required unless <see cref="MediaUrls"/> is provided.
    /// </summary>
    [JsonPropertyName("text")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Text { get; set; }

    /// <summary>
    /// Sending number (E.164). Omit to use your workspace's default sending
    /// number. When provided it must be an MMS-enabled, 10DLC-registered number
    /// you own.
    /// </summary>
    [JsonPropertyName("from")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? From { get; set; }

    /// <summary>
    /// HTTPS media URLs to attach. Required unless <see cref="Text"/> is provided.
    /// </summary>
    [JsonPropertyName("mediaUrls")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<string>? MediaUrls { get; set; }

    /// <summary>
    /// Message type for compliance. Group MMS defaults to "transactional"; pass
    /// "marketing" to apply quiet-hours rules.
    /// </summary>
    [JsonPropertyName("messageType")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? MessageType { get; set; }

    /// <summary>
    /// Creates a new group message request.
    /// </summary>
    public SendGroupMessageRequest(IEnumerable<string> to, string? text = null, List<string>? mediaUrls = null, string? from = null, string? messageType = null)
    {
        To = to.ToList();
        Text = text;
        MediaUrls = mediaUrls;
        From = from;
        MessageType = messageType;
    }

    /// <summary>
    /// Creates an empty group message request (initialize properties directly).
    /// </summary>
    public SendGroupMessageRequest()
    {
    }
}

/// <summary>
/// Response from sending a group MMS.
/// </summary>
public class GroupMessageResponse
{
    /// <summary>
    /// Message id — matches the id in delivery webhooks.
    /// </summary>
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// Delivery status ("sent" on a live send, "delivered" when simulated).
    /// </summary>
    [JsonPropertyName("status")]
    public string Status { get; set; } = string.Empty;

    /// <summary>
    /// The recipients the group message was sent to.
    /// </summary>
    [JsonPropertyName("to")]
    public List<string> To { get; set; } = new();

    /// <summary>
    /// Identifier for the group conversation. Present on live sends.
    /// </summary>
    [JsonPropertyName("group_message_id")]
    public string? GroupMessageId { get; set; }

    /// <summary>
    /// True when the send was simulated (test key, or before your account's
    /// domestic verification is approved) and nothing was sent to the carrier.
    /// </summary>
    [JsonPropertyName("simulated")]
    public bool? Simulated { get; set; }

    /// <summary>
    /// Human-readable note, present on simulated sends.
    /// </summary>
    [JsonPropertyName("message")]
    public string? Message { get; set; }

    /// <summary>
    /// Creates a GroupMessageResponse from a JSON element.
    /// </summary>
    internal static GroupMessageResponse FromJson(JsonElement element, JsonSerializerOptions options)
    {
        return JsonSerializer.Deserialize<GroupMessageResponse>(element.GetRawText(), options)
            ?? new GroupMessageResponse();
    }
}
