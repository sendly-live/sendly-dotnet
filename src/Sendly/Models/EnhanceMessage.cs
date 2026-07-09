using System.Text.Json;
using System.Text.Json.Serialization;

namespace Sendly.Models;

/// <summary>
/// Request to AI-enhance a draft message. Provide <see cref="Text"/>,
/// <see cref="MessageType"/>, or both — at least one is required.
/// </summary>
public class EnhanceMessageRequest
{
    /// <summary>
    /// Draft message text to rewrite. Optional if <see cref="MessageType"/> is
    /// provided (the model then generates a suitable message for that type).
    /// Only the first 500 characters are considered; the result is trimmed to
    /// one SMS segment.
    /// </summary>
    [JsonPropertyName("text")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Text { get; set; }

    /// <summary>
    /// Hint about the kind of message so the rewrite is targeted (e.g.
    /// "marketing", "transactional"). Optional if <see cref="Text"/> is provided.
    /// </summary>
    [JsonPropertyName("messageType")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? MessageType { get; set; }

    /// <summary>
    /// Creates a new enhance request.
    /// </summary>
    public EnhanceMessageRequest(string? text = null, string? messageType = null)
    {
        Text = text;
        MessageType = messageType;
    }

    /// <summary>
    /// Creates an empty enhance request (initialize properties directly).
    /// </summary>
    public EnhanceMessageRequest()
    {
    }
}

/// <summary>
/// Result of an AI message enhancement.
/// </summary>
public class EnhanceMessageResponse
{
    /// <summary>
    /// The rewritten message, capped at 160 characters (one SMS segment). When
    /// AI enhancement is unavailable, this falls back to the original text.
    /// </summary>
    [JsonPropertyName("enhanced")]
    public string Enhanced { get; set; } = string.Empty;

    /// <summary>
    /// Short explanation of what changed. An empty string on the fallback path.
    /// </summary>
    [JsonPropertyName("explanation")]
    public string Explanation { get; set; } = string.Empty;

    /// <summary>
    /// The model that produced the enhancement, when available.
    /// </summary>
    [JsonPropertyName("model")]
    public string? Model { get; set; }

    /// <summary>
    /// Creates an EnhanceMessageResponse from a JSON element.
    /// </summary>
    internal static EnhanceMessageResponse FromJson(JsonElement element, JsonSerializerOptions options)
    {
        return JsonSerializer.Deserialize<EnhanceMessageResponse>(element.GetRawText(), options)
            ?? new EnhanceMessageResponse();
    }
}
