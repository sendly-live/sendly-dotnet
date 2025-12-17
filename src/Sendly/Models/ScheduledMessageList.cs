using System.Collections;
using System.Text.Json;

namespace Sendly.Models;

/// <summary>
/// A paginated list of scheduled messages.
/// </summary>
public class ScheduledMessageList : IEnumerable<ScheduledMessage>
{
    private readonly List<ScheduledMessage> _messages;

    /// <summary>
    /// The scheduled messages in this page.
    /// </summary>
    public IReadOnlyList<ScheduledMessage> Data => _messages;

    /// <summary>
    /// Total number of scheduled messages.
    /// </summary>
    public int Total { get; }

    /// <summary>
    /// The limit used for this request.
    /// </summary>
    public int Limit { get; }

    /// <summary>
    /// The offset used for this request.
    /// </summary>
    public int Offset { get; }

    /// <summary>
    /// Whether there are more results.
    /// </summary>
    public bool HasMore { get; }

    internal ScheduledMessageList(JsonDocument doc, JsonSerializerOptions options)
    {
        _messages = new List<ScheduledMessage>();
        var root = doc.RootElement;

        if (root.TryGetProperty("data", out var dataElement) && dataElement.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in dataElement.EnumerateArray())
            {
                _messages.Add(ScheduledMessage.FromJson(item, options));
            }
        }

        Total = root.TryGetProperty("total", out var totalProp) ? totalProp.GetInt32() : _messages.Count;
        Limit = root.TryGetProperty("limit", out var limitProp) ? limitProp.GetInt32() : 20;
        Offset = root.TryGetProperty("offset", out var offsetProp) ? offsetProp.GetInt32() : 0;
        HasMore = root.TryGetProperty("has_more", out var hasMoreProp)
            ? hasMoreProp.GetBoolean()
            : (Offset + _messages.Count < Total);
    }

    public IEnumerator<ScheduledMessage> GetEnumerator() => _messages.GetEnumerator();
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
