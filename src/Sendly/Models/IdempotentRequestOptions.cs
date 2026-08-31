namespace Sendly.Models;

/// <summary>
/// Per-call options accepted by mutating resource methods.
/// </summary>
public class IdempotentRequestOptions
{
    /// <summary>
    /// Idempotency key for this operation (1-255 printable ASCII characters).
    ///
    /// The SDK already generates a key per logical request automatically, so
    /// the server can dedupe the SDK's own timeout retries. Supply your own
    /// key when you need idempotency across process restarts or your own
    /// retry loops — repeating a request with the same key within 24 hours
    /// returns the original result instead of executing again.
    /// </summary>
    public string? IdempotencyKey { get; set; }
}
