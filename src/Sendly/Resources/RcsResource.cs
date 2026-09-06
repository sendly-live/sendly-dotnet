using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using Sendly.Exceptions;
using Sendly.Models;

namespace Sendly.Resources;

/// <summary>
/// RCS Resource — register your brand and agent, discover your agents, and
/// pre-flight recipient capability.
///
/// RCS is a first-class Sendly channel: branded rich messaging (cards and
/// suggestion chips) delivered over RCS when the recipient's device supports
/// it, with an automatic SMS fallback (billed as SMS) for plain-text sends when
/// it doesn't. Send with
/// <c>client.Messages.SendAsync(new SendRcsMessageRequest(...))</c>.
///
/// Sending as your brand requires an RCS agent — the verified sender identity
/// recipients see. Registration is self-serve, from the dashboard or this API,
/// and follows one path:
///
/// 1. Brand — draft your business identity with
/// <see cref="RcsBrandsResource.CreateAsync"/> (<see cref="RcsDossierResource.GetAsync"/>
/// prefills it from details already on file). US businesses only for now.
///
/// 2. Agent — draft the sender identity under that brand with
/// <see cref="RcsAgentsResource.CreateAsync"/>: name, use case, description,
/// logo, hero, colour, and policy links.
///
/// 3. Submit — <see cref="RcsAgentsResource.SubmitAsync"/> sends brand and
/// agent to Sendly for review, then to the carrier network. Poll
/// <see cref="RcsAgentsResource.GetAsync"/> or <see cref="RcsRegistrationResource.GetAsync"/>
/// for the stage as it moves through review (<see cref="RcsCustomerStage"/>).
///
/// 4. Test — once the stage is <c>testing</c>, invite your own devices with
/// <see cref="RcsAgentsResource.SetTestDevicesAsync"/> and fill in the campaign
/// (message examples, consent) with <see cref="RcsAgentsResource.UpdateAsync"/>.
///
/// 5. Launch — <see cref="RcsAgentsResource.RequestLaunchAsync"/> asks Sendly to
/// launch the agent with the carrier network. Once the agent is
/// <c>sendable</c>, no other setup is needed.
///
/// Logo, hero, and call-to-action media must already be public
/// <c>https://</c> URLs; uploading assets is dashboard-only. Reads need an API
/// key with the <c>rcs:read</c> scope and writes <c>rcs:write</c>. Writes
/// accept an optional <see cref="IdempotentRequestOptions"/>; POST requests
/// get an idempotency key automatically. Every registration call answers 404
/// (<see cref="NotFoundException"/>, <c>rcs_not_enabled</c>) until the
/// <c>rcs_channel</c> flag is on for your account.
///
/// An agent with status <c>testing</c> reaches invited test numbers;
/// <c>approved</c> reaches everyone. Sends and capability checks require a
/// live API key.
/// </summary>
/// <example>
/// <code>
/// // Register: brand -> agent -> submit (then test and request launch)
/// var brand = (await client.Rcs.Brands.CreateAsync(new RcsBrandInput
/// {
///     DisplayName = "Acme Coffee",
///     LegalName = "Acme Coffee LLC",
///     Ein = "12-3456789",
///     Address = new RcsBrandAddressInput { Line1 = "100 Main St", City = "Chicago", State = "IL", PostalCode = "60601", CountryCode = "US" },
/// })).Brand;
/// var agent = (await client.Rcs.Agents.CreateAsync(new CreateRcsAgentRequest
/// {
///     BrandId = brand.Id,
///     DisplayName = "Acme Coffee",
///     UseCase = RcsAgentUseCase.MultiUse,
///     Basics = new RcsAgentBasicsInput { LogoUrl = "https://acme.example/rcs/logo.png" },
/// })).Agent;
/// await client.Rcs.Agents.SubmitAsync(agent.Id);
///
/// // Send once an agent is sendable
/// var agents = await client.Rcs.Agents.ListAsync();
/// var capability = await client.Rcs.CapabilityAsync("+15551234567");
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
    /// The RCS agents on your workspace — list, draft, submit, test, and
    /// launch the brand identities you send as.
    /// </summary>
    public RcsAgentsResource Agents { get; }

    /// <summary>
    /// Draft and update the brand an agent is registered under.
    /// </summary>
    public RcsBrandsResource Brands { get; }

    /// <summary>
    /// The workspace's registration at a glance.
    /// </summary>
    public RcsRegistrationResource Registration { get; }

    /// <summary>
    /// Business details already on file, ready to prefill a brand.
    /// </summary>
    public RcsDossierResource Dossier { get; }

    public RcsResource(SendlyClient client)
    {
        _client = client;
        Agents = new RcsAgentsResource(client);
        Brands = new RcsBrandsResource(client);
        Registration = new RcsRegistrationResource(client);
        Dossier = new RcsDossierResource(client);
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
/// The workspace's RCS registration at a glance.
/// </summary>
public class RcsRegistrationResource
{
    private readonly SendlyClient _client;

    public RcsRegistrationResource(SendlyClient client)
    {
        _client = client;
    }

    /// <summary>
    /// Fetch the workspace's registration at a glance: the newest agent, its
    /// brand and test devices, and the overall stage. Requires the
    /// <c>rcs:read</c> scope.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The current brand, agent, devices, and stage (<c>draft</c> when nothing exists)</returns>
    /// <exception cref="NotFoundException">404 <c>rcs_not_enabled</c> when RCS registration isn't enabled for the account</exception>
    public async Task<RcsRegistration> GetAsync(
        CancellationToken cancellationToken = default)
    {
        using var doc = await _client.GetAsync("/rcs/registration", null, cancellationToken);
        return JsonSerializer.Deserialize<RcsRegistration>(doc.RootElement.GetRawText(), _client.JsonOptions)!;
    }
}

/// <summary>
/// Business details already on file, ready to prefill an RCS brand.
/// </summary>
public class RcsDossierResource
{
    private readonly SendlyClient _client;

    public RcsDossierResource(SendlyClient client)
    {
        _client = client;
    }

    /// <summary>
    /// Fetch business details already on file (from 10DLC or toll-free
    /// verification), shaped for <see cref="RcsBrandsResource.CreateAsync"/>.
    /// Requires the <c>rcs:read</c> scope.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Prefilled brand fields, where they came from, and US eligibility</returns>
    /// <exception cref="NotFoundException">404 <c>rcs_not_enabled</c> when RCS registration isn't enabled for the account</exception>
    public async Task<RcsDossier> GetAsync(
        CancellationToken cancellationToken = default)
    {
        using var doc = await _client.GetAsync("/rcs/dossier", null, cancellationToken);
        return JsonSerializer.Deserialize<RcsDossier>(doc.RootElement.GetRawText(), _client.JsonOptions)!;
    }
}

/// <summary>
/// Draft and update the brand an RCS agent is registered under.
/// </summary>
public class RcsBrandsResource
{
    private readonly SendlyClient _client;

    public RcsBrandsResource(SendlyClient client)
    {
        _client = client;
    }

    /// <summary>
    /// Draft a brand — step 1 of registering for RCS. Requires the
    /// <c>rcs:write</c> scope.
    ///
    /// Every field is optional while drafting; required-field checks run at
    /// <see cref="RcsAgentsResource.SubmitAsync"/>. <c>Address.CountryCode</c>
    /// must be <c>US</c> — RCS registration is available to US businesses for
    /// now. An idempotency key is generated automatically; pass
    /// <paramref name="options"/> to supply your own.
    /// </summary>
    /// <param name="request">Business identity details</param>
    /// <param name="options">Optional idempotency key</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The created brand (<c>ReviewStatus</c> "draft")</returns>
    /// <exception cref="ValidationException">422 <c>rcs_us_only</c> when the address is outside the US</exception>
    /// <exception cref="NotFoundException">404 <c>rcs_not_enabled</c> when RCS registration isn't enabled for the account</exception>
    public async Task<RcsBrandResponse> CreateAsync(
        RcsBrandInput request,
        IdempotentRequestOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        if (request == null)
            throw new ValidationException("Brand details are required");

        using var doc = await _client.PostAsync("/rcs/brands", request, options?.IdempotencyKey, true, cancellationToken);
        return JsonSerializer.Deserialize<RcsBrandResponse>(doc.RootElement.GetRawText(), _client.JsonOptions)!;
    }

    /// <summary>
    /// Update a brand draft. Requires the <c>rcs:write</c> scope.
    ///
    /// Only the fields you set are changed; send an empty string to clear a
    /// text field, and <c>Address</c> / <c>Contact</c> may be partial. A brand
    /// is locked while Sendly is reviewing it (<c>awaiting_review</c>,
    /// <c>launch_requested</c>) and once the carrier network has registered
    /// it. No idempotency key is sent unless you pass <paramref name="options"/>.
    /// </summary>
    /// <param name="id">Brand identifier</param>
    /// <param name="request">Fields to change</param>
    /// <param name="options">Optional idempotency key</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The updated brand</returns>
    /// <exception cref="NotFoundException">404 <c>rcs_not_found</c> when the brand isn't in this workspace</exception>
    /// <exception cref="ValidationException">422 <c>rcs_us_only</c> or <c>rcs_invalid_content</c> (see <see cref="SendlyException.FieldErrors"/>)</exception>
    /// <exception cref="SendlyException">409 <c>rcs_field_locked</c> while the brand is under review</exception>
    public async Task<RcsBrandResponse> UpdateAsync(
        string id,
        RcsBrandInput request,
        IdempotentRequestOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(id))
            throw new ValidationException("Brand ID is required");
        if (request == null)
            throw new ValidationException("Brand details are required");

        using var doc = await _client.PatchAsync($"/rcs/brands/{Uri.EscapeDataString(id)}", request, options?.IdempotencyKey, cancellationToken);
        return JsonSerializer.Deserialize<RcsBrandResponse>(doc.RootElement.GetRawText(), _client.JsonOptions)!;
    }
}

/// <summary>
/// The RCS agents on your workspace — list, draft, submit, test, and launch.
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
    /// list means no agent is registered yet — draft one with
    /// <see cref="CreateAsync"/> or from the dashboard.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Your agents with status, stage, and sendability</returns>
    public async Task<RcsAgentsListResponse> ListAsync(
        CancellationToken cancellationToken = default)
    {
        using var doc = await _client.GetAsync("/rcs/agents", null, cancellationToken);
        return JsonSerializer.Deserialize<RcsAgentsListResponse>(doc.RootElement.GetRawText(), _client.JsonOptions)!;
    }

    /// <summary>
    /// Draft an agent under a brand — step 2 of registering for RCS.
    /// Requires the <c>rcs:write</c> scope.
    ///
    /// <c>LogoUrl</c>, <c>HeroUrl</c>, and <c>CallToActionMediaUrl</c> must be
    /// public <c>https://</c> URLs; uploading assets is dashboard-only. The
    /// campaign and testing sections can be filled in later with
    /// <see cref="UpdateAsync"/>. An idempotency key is generated
    /// automatically; pass <paramref name="options"/> to supply your own.
    /// </summary>
    /// <param name="request">The brand to register under, plus the agent identity. <see cref="CreateRcsAgentRequest.BrandId"/> is required.</param>
    /// <param name="options">Optional idempotency key</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The created agent (<c>ReviewStatus</c> "draft")</returns>
    /// <exception cref="NotFoundException">404 <c>rcs_not_found</c> when the brand isn't in this workspace</exception>
    /// <exception cref="ValidationException">422 <c>rcs_invalid_content</c> when a media URL isn't https (see <see cref="SendlyException.FieldErrors"/>)</exception>
    public async Task<RcsAgentResponse> CreateAsync(
        CreateRcsAgentRequest request,
        IdempotentRequestOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(request?.BrandId))
            throw new ValidationException("Brand ID is required");

        using var doc = await _client.PostAsync("/rcs/agents", request, options?.IdempotencyKey, true, cancellationToken);
        return JsonSerializer.Deserialize<RcsAgentResponse>(doc.RootElement.GetRawText(), _client.JsonOptions)!;
    }

    /// <summary>
    /// Fetch one agent with its review state and invited devices. Poll this
    /// to follow the stage through review, testing, and launch. Requires the
    /// <c>rcs:read</c> scope.
    /// </summary>
    /// <param name="id">Agent identifier</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The agent, its devices, and where the registration sits</returns>
    /// <exception cref="NotFoundException">404 <c>rcs_not_found</c> when the agent isn't in this workspace</exception>
    public async Task<RcsAgentDetailResponse> GetAsync(
        string id,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(id))
            throw new ValidationException("Agent ID is required");

        using var doc = await _client.GetAsync($"/rcs/agents/{Uri.EscapeDataString(id)}", null, cancellationToken);
        return JsonSerializer.Deserialize<RcsAgentDetailResponse>(doc.RootElement.GetRawText(), _client.JsonOptions)!;
    }

    /// <summary>
    /// Update an agent draft. Requires the <c>rcs:write</c> scope.
    ///
    /// Only the sections you set are changed: <c>DisplayName</c>,
    /// <c>UseCase</c>, and <c>Basics</c> merge into the identity;
    /// <c>Campaign</c> and <c>Testing</c> merge field-wise, and
    /// <see cref="UpdateRcsAgentRequest.ClearCampaign"/> /
    /// <see cref="UpdateRcsAgentRequest.ClearTesting"/> remove a section. An
    /// agent is locked while Sendly is reviewing it; the identity locks once
    /// sent to the carrier network, and the campaign and testing sections lock
    /// once the launch is sent (unless it was declined). No idempotency key is
    /// sent unless you pass <paramref name="options"/>.
    /// </summary>
    /// <param name="id">Agent identifier</param>
    /// <param name="request">Sections to change</param>
    /// <param name="options">Optional idempotency key</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The updated agent</returns>
    /// <exception cref="NotFoundException">404 <c>rcs_not_found</c> when the agent isn't in this workspace</exception>
    /// <exception cref="ValidationException">422 <c>rcs_invalid_content</c> (see <see cref="SendlyException.FieldErrors"/>)</exception>
    /// <exception cref="SendlyException">409 <c>rcs_field_locked</c> while the section is locked</exception>
    public async Task<RcsAgentResponse> UpdateAsync(
        string id,
        UpdateRcsAgentRequest request,
        IdempotentRequestOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(id))
            throw new ValidationException("Agent ID is required");
        if (request == null)
            throw new ValidationException("Agent details are required");

        object body = request;
        if (request.ClearCampaign || request.ClearTesting)
        {
            var node = JsonSerializer.SerializeToNode(request, _client.JsonOptions)!.AsObject();
            if (request.ClearCampaign)
                node["campaign"] = null;
            if (request.ClearTesting)
                node["testing"] = null;
            body = node;
        }

        using var doc = await _client.PatchAsync($"/rcs/agents/{Uri.EscapeDataString(id)}", body, options?.IdempotencyKey, cancellationToken);
        return JsonSerializer.Deserialize<RcsAgentResponse>(doc.RootElement.GetRawText(), _client.JsonOptions)!;
    }

    /// <summary>
    /// Replace the agent's test devices (up to 20). Requires the
    /// <c>rcs:write</c> scope.
    ///
    /// The list is authoritative: numbers missing from it are removed, new
    /// ones are invited. Devices receive an invite from the carrier network
    /// once the agent reaches the <c>testing</c> stage. No idempotency key is
    /// sent unless you pass <paramref name="options"/>.
    /// </summary>
    /// <param name="id">Agent identifier</param>
    /// <param name="devices">The full list of devices to keep invited</param>
    /// <param name="options">Optional idempotency key</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The full device list after the change</returns>
    /// <exception cref="NotFoundException">404 <c>rcs_not_found</c> when the agent isn't in this workspace</exception>
    /// <exception cref="ValidationException">422 <c>rcs_invalid_content</c> naming the bad <c>devices.&lt;i&gt;.phoneNumber</c> in <see cref="SendlyException.FieldErrors"/></exception>
    /// <exception cref="SendlyException">409 <c>rcs_field_locked</c> while the agent is under review</exception>
    public async Task<RcsTestDeviceListResponse> SetTestDevicesAsync(
        string id,
        IEnumerable<RcsTestDeviceInput> devices,
        IdempotentRequestOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(id))
            throw new ValidationException("Agent ID is required");
        if (devices == null)
            throw new ValidationException("Devices are required");

        var list = devices.ToList();
        if (list.Any(device => string.IsNullOrEmpty(device?.PhoneNumber)))
            throw new ValidationException("Device phone number is required");

        using var doc = await _client.PutAsync($"/rcs/agents/{Uri.EscapeDataString(id)}/test-devices", new { devices = list }, options?.IdempotencyKey, cancellationToken);
        return JsonSerializer.Deserialize<RcsTestDeviceListResponse>(doc.RootElement.GetRawText(), _client.JsonOptions)!;
    }

    /// <summary>
    /// Submit the agent and its brand to Sendly for review — step 3 of
    /// registering for RCS. Requires the <c>rcs:write</c> scope.
    ///
    /// Required-field checks run here: the brand and the agent identity must
    /// be complete, and media URLs must be public <c>https://</c>. On success
    /// the agent moves to <c>in_review</c>; Sendly reviews it, then the
    /// carrier network. Poll <see cref="GetAsync"/> to follow progress. An
    /// idempotency key is generated automatically; pass your own through
    /// <paramref name="options"/> so a retried call returns the original
    /// result instead of notifying reviewers again.
    /// </summary>
    /// <param name="id">Agent identifier</param>
    /// <param name="options">Optional idempotency key</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The agent with <c>ReviewStatus</c> "awaiting_review" and the new stage</returns>
    /// <exception cref="NotFoundException">404 <c>rcs_not_found</c> when the agent isn't in this workspace</exception>
    /// <exception cref="ValidationException">422 <c>rcs_invalid_content</c> listing <c>brand.&lt;field&gt;</c> / <c>agent.&lt;field&gt;</c> gaps in <see cref="SendlyException.FieldErrors"/></exception>
    /// <exception cref="SendlyException">409 <c>rcs_field_locked</c> when already submitted, or <c>rcs_brand_not_verified</c> when the carrier network declined the brand</exception>
    public async Task<RcsAgentReviewResponse> SubmitAsync(
        string id,
        IdempotentRequestOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(id))
            throw new ValidationException("Agent ID is required");

        using var doc = await _client.PostAsync($"/rcs/agents/{Uri.EscapeDataString(id)}/submit", new { }, options?.IdempotencyKey, true, cancellationToken);
        return JsonSerializer.Deserialize<RcsAgentReviewResponse>(doc.RootElement.GetRawText(), _client.JsonOptions)!;
    }

    /// <summary>
    /// Ask Sendly to launch the agent — step 5, once you've tested it on an
    /// invited device. Requires the <c>rcs:write</c> scope.
    ///
    /// The campaign section must be complete (an overview, at least one
    /// interaction, at least three message examples, consent settings) and
    /// the testing section needs a test URL, which you can pass here. On
    /// success the agent moves to <c>launch_review</c>; Sendly reviews it,
    /// then launches it with the carrier network. Poll <see cref="GetAsync"/>
    /// until the stage is <c>live</c>. An idempotency key is generated
    /// automatically; pass <paramref name="options"/> to supply your own.
    /// </summary>
    /// <param name="id">Agent identifier</param>
    /// <param name="request">Optional testing details saved before the request</param>
    /// <param name="options">Optional idempotency key</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The agent with <c>ReviewStatus</c> "launch_requested" and the new stage</returns>
    /// <exception cref="NotFoundException">404 <c>rcs_not_found</c> when the agent isn't in this workspace</exception>
    /// <exception cref="ValidationException">422 <c>rcs_invalid_content</c> listing <c>campaign.&lt;field&gt;</c> / <c>testing.&lt;field&gt;</c> gaps in <see cref="SendlyException.FieldErrors"/></exception>
    /// <exception cref="SendlyException">409 <c>rcs_launch_not_ready</c> before the agent reaches testing, or <c>rcs_field_locked</c> while a request is already under review</exception>
    public async Task<RcsAgentReviewResponse> RequestLaunchAsync(
        string id,
        RcsRequestLaunchRequest? request = null,
        IdempotentRequestOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(id))
            throw new ValidationException("Agent ID is required");

        using var doc = await _client.PostAsync($"/rcs/agents/{Uri.EscapeDataString(id)}/request-launch", request ?? new RcsRequestLaunchRequest(), options?.IdempotencyKey, true, cancellationToken);
        return JsonSerializer.Deserialize<RcsAgentReviewResponse>(doc.RootElement.GetRawText(), _client.JsonOptions)!;
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

    /// <summary>Where the registration sits, in customer terms; see <see cref="RcsCustomerStage"/>.</summary>
    [JsonPropertyName("stage")]
    public string? Stage { get; set; }

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
