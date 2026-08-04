using System.Text.Json;
using System.Text.Json.Serialization;

namespace Sendly.Resources;

/// <summary>
/// RCS Resource — discover your agents and pre-flight recipient capability.
///
/// RCS is a first-class Sendly channel: branded rich messaging (cards and
/// suggestion chips) delivered over RCS when the recipient's device supports
/// it, with an automatic SMS fallback (billed as SMS) for plain-text sends when
/// it doesn't. Send with
/// <c>client.Messages.SendAsync(new SendRcsMessageRequest(...))</c>.
///
/// Sending as a brand requires an RCS agent registered on your workspace —
/// contact support to register one. An agent with status <c>testing</c> reaches
/// invited test numbers; <c>approved</c> reaches everyone. Sends and capability
/// checks require a live API key.
/// </summary>
/// <example>
/// <code>
/// // 1. Find your agent
/// var agents = await client.Rcs.Agents.ListAsync();
///
/// // 2. Optional pre-flight: can this recipient receive RCS?
/// var capability = await client.Rcs.CapabilityAsync("+15551234567");
///
/// // 3. Send — text falls back to SMS for non-RCS recipients
/// var message = await client.Messages.SendAsync(new SendRcsMessageRequest(
///     "+15551234567",
///     text: "Your order has shipped!"
/// ));
/// </code>
/// </example>
public class RcsResource
{
    private readonly SendlyClient _client;

    /// <summary>
    /// The RCS agents on your workspace — the brand identities you send as.
    /// </summary>
    public RcsAgentsResource Agents { get; }

    public RcsResource(SendlyClient client)
    {
        _client = client;
        Agents = new RcsAgentsResource(client);
    }

    /// <summary>
    /// Check whether a recipient can receive RCS from one of your agents.
    ///
    /// A not-capable recipient still receives plain-text sends via the SMS
    /// fallback; card sends to them fail with 422
    /// <c>rcs_not_supported_for_recipient</c>. Pass <paramref name="agentId"/>
    /// when your workspace has more than one agent. Requires a live API key.
    /// </summary>
    /// <param name="to">The recipient's number, in E.164 format</param>
    /// <param name="agentId">The agent to check against; omit to use your workspace's only agent</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Whether the recipient is RCS-capable and which features their device reports</returns>
    public async Task<RcsCapability> CapabilityAsync(
        string to,
        string? agentId = null,
        CancellationToken cancellationToken = default)
    {
        WhatsAppResource.ValidatePhone(to);

        var queryParams = new Dictionary<string, string> { ["to"] = to };
        if (!string.IsNullOrEmpty(agentId))
            queryParams["agentId"] = agentId;

        using var doc = await _client.GetAsync("/rcs/capability", queryParams, cancellationToken);
        return JsonSerializer.Deserialize<RcsCapability>(doc.RootElement.GetRawText(), _client.JsonOptions)!;
    }
}

/// <summary>
/// The RCS agents on your workspace.
/// </summary>
public class RcsAgentsResource
{
    private readonly SendlyClient _client;

    public RcsAgentsResource(SendlyClient client)
    {
        _client = client;
    }

    /// <summary>
    /// List your RCS agents.
    ///
    /// Returns the agents registered on your workspace, newest first. An empty
    /// list means no agent is registered yet — contact support to register one
    /// for your brand.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Your agents with status and sendability</returns>
    public async Task<RcsAgentsListResponse> ListAsync(
        CancellationToken cancellationToken = default)
    {
        using var doc = await _client.GetAsync("/rcs/agents", null, cancellationToken);
        return JsonSerializer.Deserialize<RcsAgentsListResponse>(doc.RootElement.GetRawText(), _client.JsonOptions)!;
    }
}

/// <summary>
/// An RCS agent — the brand identity your RCS messages are sent as.
/// </summary>
public class RcsAgent
{
    /// <summary>Unique agent identifier — pass as <c>AgentId</c> on sends.</summary>
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    /// <summary>The display name recipients see.</summary>
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Agent status. <c>testing</c> reaches invited test numbers only;
    /// <c>approved</c> reaches everyone.
    /// </summary>
    [JsonPropertyName("status")]
    public string Status { get; set; } = string.Empty;

    /// <summary>The agent's registered use case, or null when not set.</summary>
    [JsonPropertyName("useCase")]
    public string? UseCase { get; set; }

    /// <summary>True when the agent can send right now.</summary>
    [JsonPropertyName("sendable")]
    public bool Sendable { get; set; }

    /// <summary>When the agent was registered (ISO 8601).</summary>
    [JsonPropertyName("createdAt")]
    public string CreatedAt { get; set; } = string.Empty;
}

/// <summary>
/// Response from <see cref="RcsAgentsResource.ListAsync"/>.
/// </summary>
public class RcsAgentsListResponse
{
    [JsonPropertyName("agents")]
    public List<RcsAgent> Agents { get; set; } = new();
}

/// <summary>
/// Response from <see cref="RcsResource.CapabilityAsync"/> — whether a
/// recipient's device can receive RCS from your agent.
/// </summary>
public class RcsCapability
{
    /// <summary>The checked number, in E.164 format.</summary>
    [JsonPropertyName("to")]
    public string To { get; set; } = string.Empty;

    /// <summary>The agent the check ran against.</summary>
    [JsonPropertyName("agentId")]
    public string AgentId { get; set; } = string.Empty;

    /// <summary>
    /// True when the recipient can receive RCS from this agent. When false, a
    /// text send would take the SMS fallback and a card send would fail with
    /// 422.
    /// </summary>
    [JsonPropertyName("capable")]
    public bool Capable { get; set; }

    /// <summary>
    /// RCS features the recipient's device reports; empty when not capable.
    /// </summary>
    [JsonPropertyName("features")]
    public List<string> Features { get; set; } = new();
}
