using System.Text.Json;
using System.Text.Json.Serialization;

namespace Sendly.Models;

/// <summary>
/// Response from a batch send operation.
/// </summary>
public class BatchMessageResponse
{
    /// <summary>
    /// Batch status constants.
    /// </summary>
    public static class Statuses
    {
        public const string Processing = "processing";
        public const string Completed = "completed";
        public const string PartialFailure = "partial_failure";
        public const string Failed = "failed";
    }

    /// <summary>
    /// Unique batch identifier. The send endpoint returns this as "batchId";
    /// the batch read and list endpoints return the same value as "id".
    /// </summary>
    [JsonPropertyName("batchId")]
    public string BatchId { get; set; } = string.Empty;

    /// <summary>
    /// Batch status.
    /// </summary>
    [JsonPropertyName("status")]
    public string Status { get; set; } = string.Empty;

    /// <summary>
    /// Total number of messages in the batch.
    /// </summary>
    [JsonPropertyName("total")]
    public int Total { get; set; }

    /// <summary>
    /// Number of messages still queued, or null when the response does not
    /// report one. Only the batch read and list endpoints report this; null
    /// on a send response.
    /// </summary>
    [JsonPropertyName("queued")]
    public int? QueuedCount { get; set; }

    /// <summary>
    /// Number of messages still queued, or 0 when the response does not
    /// report one.
    /// </summary>
    [Obsolete("Use QueuedCount, which is null when the response does not report a queued count. This property returns 0 in that case and so cannot tell \"zero queued\" apart from \"not reported\".")]
    [JsonIgnore]
    public int Queued
    {
        get => QueuedCount ?? 0;
        set => QueuedCount = value;
    }

    /// <summary>
    /// Number of messages handed off for delivery.
    /// </summary>
    [JsonPropertyName("sent")]
    public int Sent { get; set; }

    /// <summary>
    /// Number of messages that failed.
    /// </summary>
    [JsonPropertyName("failed")]
    public int Failed { get; set; }

    /// <summary>
    /// Number of recipients skipped because they had opted out.
    /// </summary>
    [JsonPropertyName("optedOutSkipped")]
    public int OptedOutSkipped { get; set; }

    /// <summary>
    /// Number of recipients skipped because the number was invalid.
    /// </summary>
    [JsonPropertyName("invalidSkipped")]
    public int InvalidSkipped { get; set; }

    /// <summary>
    /// Total credits used.
    /// </summary>
    [JsonPropertyName("creditsUsed")]
    public int CreditsUsed { get; set; }

    /// <summary>
    /// Credits refunded for messages that were reserved but not sent.
    /// </summary>
    [JsonPropertyName("creditsRefunded")]
    public int CreditsRefunded { get; set; }

    /// <summary>
    /// Individual message results.
    /// </summary>
    [JsonPropertyName("messages")]
    public List<BatchMessageResult> Messages { get; set; } = new();

    /// <summary>
    /// Creation timestamp. Only the batch read and list endpoints report
    /// this; null on a send response.
    /// </summary>
    [JsonPropertyName("createdAt")]
    public DateTime? CreatedAt { get; set; }

    /// <summary>
    /// Completion timestamp.
    /// </summary>
    [JsonPropertyName("completedAt")]
    public DateTime? CompletedAt { get; set; }

    /// <summary>
    /// Whether the batch is still processing.
    /// </summary>
    public bool IsProcessing => Status == Statuses.Processing;

    /// <summary>
    /// Whether the batch completed successfully.
    /// </summary>
    public bool IsCompleted => Status == Statuses.Completed;

    /// <summary>
    /// Whether the batch completed with some failures.
    /// </summary>
    public bool IsPartialFailure => Status == Statuses.PartialFailure;

    /// <summary>
    /// Whether all messages in the batch failed.
    /// </summary>
    public bool IsFailed => Status == Statuses.Failed;

    /// <summary>
    /// Creates a BatchMessageResponse from a JSON element.
    /// </summary>
    internal static BatchMessageResponse FromJson(JsonElement element, JsonSerializerOptions options)
    {
        var batch = JsonSerializer.Deserialize<BatchMessageResponse>(element.GetRawText(), options)
            ?? new BatchMessageResponse();

        if (batch.BatchId.Length == 0
            && element.ValueKind == JsonValueKind.Object
            && element.TryGetProperty("id", out var idProp)
            && idProp.ValueKind == JsonValueKind.String)
        {
            batch.BatchId = idProp.GetString() ?? string.Empty;
        }

        return batch;
    }
}
