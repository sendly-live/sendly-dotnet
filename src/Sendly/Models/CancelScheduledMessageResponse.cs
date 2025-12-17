using System.Text.Json;
using System.Text.Json.Serialization;

namespace Sendly.Models;

/// <summary>
/// Response from cancelling a scheduled message.
/// </summary>
public class CancelScheduledMessageResponse
{
    /// <summary>
    /// The cancelled message ID.
    /// </summary>
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// The new status (should be "cancelled").
    /// </summary>
    [JsonPropertyName("status")]
    public string Status { get; set; } = string.Empty;

    /// <summary>
    /// Credits refunded from cancellation.
    /// </summary>
    [JsonPropertyName("credits_refunded")]
    public int CreditsRefunded { get; set; }

    /// <summary>
    /// Cancellation timestamp.
    /// </summary>
    [JsonPropertyName("cancelled_at")]
    public DateTime? CancelledAt { get; set; }

    /// <summary>
    /// Creates a CancelScheduledMessageResponse from a JSON element.
    /// </summary>
    internal static CancelScheduledMessageResponse FromJson(JsonElement element, JsonSerializerOptions options)
    {
        return JsonSerializer.Deserialize<CancelScheduledMessageResponse>(element.GetRawText(), options)
            ?? new CancelScheduledMessageResponse();
    }
}
