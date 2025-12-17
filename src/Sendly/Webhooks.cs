using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Sendly;

/// <summary>
/// Webhook utilities for verifying and parsing Sendly webhook events.
/// </summary>
/// <example>
/// <code>
/// // In your ASP.NET Core controller
/// [HttpPost("/webhooks/sendly")]
/// public IActionResult HandleWebhook(
///     [FromBody] string payload,
///     [FromHeader(Name = "X-Sendly-Signature")] string signature)
/// {
///     try
///     {
///         var webhookEvent = Webhooks.ParseEvent(payload, signature, _webhookSecret);
///
///         switch (webhookEvent.Type)
///         {
///             case "message.delivered":
///                 Console.WriteLine($"Message delivered: {webhookEvent.Data.MessageId}");
///                 break;
///             case "message.failed":
///                 Console.WriteLine($"Message failed: {webhookEvent.Data.Error}");
///                 break;
///         }
///
///         return Ok();
///     }
///     catch (WebhookSignatureException)
///     {
///         return Unauthorized();
///     }
/// }
/// </code>
/// </example>
public static class Webhooks
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        PropertyNameCaseInsensitive = true
    };

    /// <summary>
    /// Verify webhook signature from Sendly.
    /// </summary>
    /// <param name="payload">Raw request body as string</param>
    /// <param name="signature">X-Sendly-Signature header value</param>
    /// <param name="secret">Your webhook secret from dashboard</param>
    /// <returns>True if signature is valid, false otherwise</returns>
    public static bool VerifySignature(string payload, string signature, string secret)
    {
        if (string.IsNullOrEmpty(payload) || string.IsNullOrEmpty(signature) || string.IsNullOrEmpty(secret))
        {
            return false;
        }

        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
        var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(payload));
        var expected = "sha256=" + Convert.ToHexString(hash).ToLowerInvariant();

        // Timing-safe comparison
        return CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(expected),
            Encoding.UTF8.GetBytes(signature)
        );
    }

    /// <summary>
    /// Parse and validate a webhook event.
    /// </summary>
    /// <param name="payload">Raw request body as string</param>
    /// <param name="signature">X-Sendly-Signature header value</param>
    /// <param name="secret">Your webhook secret from dashboard</param>
    /// <returns>Parsed and validated WebhookEvent</returns>
    /// <exception cref="WebhookSignatureException">If signature is invalid or payload is malformed</exception>
    public static WebhookEvent ParseEvent(string payload, string signature, string secret)
    {
        if (!VerifySignature(payload, signature, secret))
        {
            throw new WebhookSignatureException("Invalid webhook signature");
        }

        try
        {
            var webhookEvent = JsonSerializer.Deserialize<WebhookEvent>(payload, JsonOptions);

            if (webhookEvent == null || string.IsNullOrEmpty(webhookEvent.Id) ||
                string.IsNullOrEmpty(webhookEvent.Type) || string.IsNullOrEmpty(webhookEvent.CreatedAt))
            {
                throw new WebhookSignatureException("Invalid event structure");
            }

            return webhookEvent;
        }
        catch (JsonException ex)
        {
            throw new WebhookSignatureException($"Failed to parse webhook payload: {ex.Message}");
        }
    }

    /// <summary>
    /// Generate a webhook signature for testing purposes.
    /// </summary>
    /// <param name="payload">The payload to sign</param>
    /// <param name="secret">The secret to use for signing</param>
    /// <returns>The signature in the format "sha256=..."</returns>
    public static string GenerateSignature(string payload, string secret)
    {
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
        var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(payload));
        return "sha256=" + Convert.ToHexString(hash).ToLowerInvariant();
    }
}

/// <summary>
/// Webhook event from Sendly.
/// </summary>
public class WebhookEvent
{
    /// <summary>Unique event ID</summary>
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    /// <summary>Event type</summary>
    [JsonPropertyName("type")]
    public string Type { get; set; } = string.Empty;

    /// <summary>Event data</summary>
    [JsonPropertyName("data")]
    public WebhookMessageData Data { get; set; } = new();

    /// <summary>When the event was created (ISO 8601)</summary>
    [JsonPropertyName("created_at")]
    public string CreatedAt { get; set; } = string.Empty;

    /// <summary>API version</summary>
    [JsonPropertyName("api_version")]
    public string ApiVersion { get; set; } = "2024-01-01";
}

/// <summary>
/// Webhook message data.
/// </summary>
public class WebhookMessageData
{
    /// <summary>The message ID</summary>
    [JsonPropertyName("message_id")]
    public string MessageId { get; set; } = string.Empty;

    /// <summary>Current message status</summary>
    [JsonPropertyName("status")]
    public string Status { get; set; } = string.Empty;

    /// <summary>Recipient phone number</summary>
    [JsonPropertyName("to")]
    public string To { get; set; } = string.Empty;

    /// <summary>Sender ID or phone number</summary>
    [JsonPropertyName("from")]
    public string From { get; set; } = string.Empty;

    /// <summary>Error message if status is 'failed' or 'undelivered'</summary>
    [JsonPropertyName("error")]
    public string? Error { get; set; }

    /// <summary>Error code if available</summary>
    [JsonPropertyName("error_code")]
    public string? ErrorCode { get; set; }

    /// <summary>When the message was delivered (ISO 8601)</summary>
    [JsonPropertyName("delivered_at")]
    public string? DeliveredAt { get; set; }

    /// <summary>When the message failed (ISO 8601)</summary>
    [JsonPropertyName("failed_at")]
    public string? FailedAt { get; set; }

    /// <summary>Number of SMS segments</summary>
    [JsonPropertyName("segments")]
    public int Segments { get; set; } = 1;

    /// <summary>Credits charged</summary>
    [JsonPropertyName("credits_used")]
    public int CreditsUsed { get; set; }
}

/// <summary>
/// Exception thrown when webhook signature verification fails.
/// </summary>
public class WebhookSignatureException : Exception
{
    public WebhookSignatureException(string message = "Invalid webhook signature")
        : base(message)
    {
    }
}
