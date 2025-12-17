using System.Collections;
using System.Text.Json;

namespace Sendly.Models;

/// <summary>
/// A paginated list of message batches.
/// </summary>
public class BatchList : IEnumerable<BatchMessageResponse>
{
    private readonly List<BatchMessageResponse> _batches;

    /// <summary>
    /// The batches in this page.
    /// </summary>
    public IReadOnlyList<BatchMessageResponse> Data => _batches;

    /// <summary>
    /// Total number of batches.
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

    internal BatchList(JsonDocument doc, JsonSerializerOptions options)
    {
        _batches = new List<BatchMessageResponse>();
        var root = doc.RootElement;

        if (root.TryGetProperty("data", out var dataElement) && dataElement.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in dataElement.EnumerateArray())
            {
                _batches.Add(BatchMessageResponse.FromJson(item, options));
            }
        }

        Total = root.TryGetProperty("total", out var totalProp) ? totalProp.GetInt32() : _batches.Count;
        Limit = root.TryGetProperty("limit", out var limitProp) ? limitProp.GetInt32() : 20;
        Offset = root.TryGetProperty("offset", out var offsetProp) ? offsetProp.GetInt32() : 0;
        HasMore = root.TryGetProperty("has_more", out var hasMoreProp)
            ? hasMoreProp.GetBoolean()
            : (Offset + _batches.Count < Total);
    }

    public IEnumerator<BatchMessageResponse> GetEnumerator() => _batches.GetEnumerator();
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
