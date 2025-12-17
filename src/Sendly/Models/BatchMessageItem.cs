using System.Text.Json.Serialization;

namespace Sendly.Models;

/// <summary>
/// Represents a single message in a batch send request.
/// </summary>
public class BatchMessageItem
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
    /// Creates a new batch message item.
    /// </summary>
    /// <param name="to">Recipient phone number in E.164 format</param>
    /// <param name="text">Message content</param>
    public BatchMessageItem(string to, string text)
    {
        To = to;
        Text = text;
    }
}
