using System.Text.Json.Serialization;

namespace Sendly.Models;

/// <summary>
/// Request to send an SMS message.
/// </summary>
public class SendMessageRequest
{
    /// <summary>
    /// Recipient phone number in E.164 format.
    /// </summary>
    [JsonPropertyName("to")]
    public string To { get; set; }

    /// <summary>
    /// Message content (max 1600 characters).
    /// </summary>
    [JsonPropertyName("text")]
    public string Text { get; set; }

    /// <summary>
    /// Creates a new send message request.
    /// </summary>
    public SendMessageRequest(string to, string text)
    {
        To = to;
        Text = text;
    }
}
