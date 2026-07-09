using System.Text.Json;
using System.Text.Json.Serialization;
using Sendly.Exceptions;

namespace Sendly.Resources;

/// <summary>
/// Links Resource — branded URL shortening.
///
/// Mint branded short links for a destination URL, list the links your
/// workspace has created (with click analytics), and enable or disable an
/// individual link (a per-link kill switch).
///
/// Branded, owned-domain short links improve deliverability — carriers filter
/// public shorteners — and give you click data for analytics. Shortening is
/// gated behind the <c>url_shortener</c> rollout flag (currently founder-only
/// and not yet publicly stable); calls return a <c>not_found</c> error until
/// the flag is on for your account.
/// </summary>
/// <example>
/// <code>
/// // Shorten a URL
/// var link = await sendly.Links.CreateAsync("https://example.com/spring-sale");
/// Console.WriteLine(link.ShortUrl); // https://sendly.live/l/Ab3xY7
///
/// // List your links with click counts
/// var listing = await sendly.Links.ListAsync(new ListShortLinksOptions { Limit = 20 });
///
/// // Kill a link
/// await sendly.Links.DisableAsync(link.Code);
/// </code>
/// </example>
public class LinksResource
{
    private readonly SendlyClient _client;

    public LinksResource(SendlyClient client)
    {
        _client = client;
    }

    /// <summary>
    /// Mint a branded short link for a destination URL. Uses your workspace's
    /// brand slug when one is configured.
    /// </summary>
    /// <param name="url">The destination URL to shorten (http/https only)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The short link (code, shortUrl, destinationUrl)</returns>
    public async Task<ShortLink> CreateAsync(string url, CancellationToken cancellationToken = default)
    {
        return await CreateAsync(new CreateShortLinkRequest { Url = url }, cancellationToken);
    }

    /// <summary>
    /// Mint a branded short link for a destination URL.
    /// </summary>
    /// <param name="request">The destination URL to shorten</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The short link (code, shortUrl, destinationUrl)</returns>
    public async Task<ShortLink> CreateAsync(CreateShortLinkRequest request, CancellationToken cancellationToken = default)
    {
        ValidateUrl(request?.Url);

        using var doc = await _client.PostAsync("/links", request, cancellationToken);
        return JsonSerializer.Deserialize<ShortLink>(doc.RootElement.GetRawText(), _client.JsonOptions)!;
    }

    /// <summary>
    /// List the short links your workspace has created, newest first, with click
    /// counts and a 14-day daily click histogram.
    /// </summary>
    /// <param name="options">Pagination options (limit 1-200, offset)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The links and a total count</returns>
    public async Task<ShortLinkListResponse> ListAsync(ListShortLinksOptions? options = null, CancellationToken cancellationToken = default)
    {
        var queryParams = new Dictionary<string, string>();
        if (options?.Limit.HasValue == true)
            queryParams["limit"] = options.Limit.Value.ToString();
        if (options?.Offset.HasValue == true)
            queryParams["offset"] = options.Offset.Value.ToString();

        using var doc = await _client.GetAsync("/links", queryParams, cancellationToken);
        return JsonSerializer.Deserialize<ShortLinkListResponse>(doc.RootElement.GetRawText(), _client.JsonOptions)!;
    }

    /// <summary>
    /// Enable or disable a short link. A disabled link's redirect returns 404
    /// until it is re-enabled.
    /// </summary>
    /// <param name="code">The short link code</param>
    /// <param name="disabled">True to disable, false to re-enable</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The link's code and new disabled state</returns>
    public async Task<ShortLinkDisabledResponse> SetDisabledAsync(string code, bool disabled, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(code))
            throw new ValidationException("A link 'code' is required");

        using var doc = await _client.PatchAsync($"/links/{Uri.EscapeDataString(code)}", new { disabled }, cancellationToken);
        return JsonSerializer.Deserialize<ShortLinkDisabledResponse>(doc.RootElement.GetRawText(), _client.JsonOptions)!;
    }

    /// <summary>
    /// Disable a short link (its redirect returns 404 until re-enabled).
    /// Convenience wrapper over <see cref="SetDisabledAsync"/>.
    /// </summary>
    public async Task<ShortLinkDisabledResponse> DisableAsync(string code, CancellationToken cancellationToken = default)
    {
        return await SetDisabledAsync(code, true, cancellationToken);
    }

    /// <summary>
    /// Re-enable a previously disabled short link. Convenience wrapper over
    /// <see cref="SetDisabledAsync"/>.
    /// </summary>
    public async Task<ShortLinkDisabledResponse> EnableAsync(string code, CancellationToken cancellationToken = default)
    {
        return await SetDisabledAsync(code, false, cancellationToken);
    }

    private static void ValidateUrl(string? url)
    {
        if (string.IsNullOrEmpty(url))
            throw new ValidationException("A destination 'url' is required");

        if (!url.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
            && !url.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            throw new ValidationException("url must be an http:// or https:// URL");
        }
    }
}

/// <summary>
/// Request to mint a branded short link.
/// </summary>
public class CreateShortLinkRequest
{
    /// <summary>
    /// Destination URL to shorten. Must be an http:// or https:// URL.
    /// </summary>
    [JsonPropertyName("url")]
    public string Url { get; set; } = string.Empty;
}

/// <summary>
/// A newly minted branded short link.
/// </summary>
public class ShortLink
{
    /// <summary>Short code (the segment after the domain, e.g. "Ab3xY7").</summary>
    [JsonPropertyName("code")]
    public string Code { get; set; } = string.Empty;

    /// <summary>Full branded short URL to share (e.g. "https://sendly.live/l/Ab3xY7").</summary>
    [JsonPropertyName("shortUrl")]
    public string ShortUrl { get; set; } = string.Empty;

    /// <summary>The destination the short link redirects to.</summary>
    [JsonPropertyName("destinationUrl")]
    public string DestinationUrl { get; set; } = string.Empty;
}

/// <summary>
/// Options for listing short links.
/// </summary>
public class ListShortLinksOptions
{
    /// <summary>Maximum number of links to return (1-200). Defaults to 50.</summary>
    public int? Limit { get; set; }

    /// <summary>Number of links to skip for pagination. Defaults to 0.</summary>
    public int? Offset { get; set; }
}

/// <summary>
/// A short link with click analytics, as returned by <see cref="LinksResource.ListAsync"/>.
/// </summary>
public class ShortLinkListItem
{
    /// <summary>Short code (the segment after the domain).</summary>
    [JsonPropertyName("code")]
    public string Code { get; set; } = string.Empty;

    /// <summary>Full branded short URL.</summary>
    [JsonPropertyName("shortUrl")]
    public string ShortUrl { get; set; } = string.Empty;

    /// <summary>The destination the short link redirects to.</summary>
    [JsonPropertyName("destinationUrl")]
    public string DestinationUrl { get; set; } = string.Empty;

    /// <summary>Workspace brand slug segment, or null when unbranded.</summary>
    [JsonPropertyName("brandSlug")]
    public string? BrandSlug { get; set; }

    /// <summary>Total human clicks recorded (link-preview bots are excluded).</summary>
    [JsonPropertyName("clickCount")]
    public int ClickCount { get; set; }

    /// <summary>Whether the link is disabled (the redirect then returns 404).</summary>
    [JsonPropertyName("disabled")]
    public bool Disabled { get; set; }

    /// <summary>ISO 3166-1 alpha-2 country of the most recent click, or null.</summary>
    [JsonPropertyName("lastCountry")]
    public string? LastCountry { get; set; }

    /// <summary>When the link was last clicked, or null.</summary>
    [JsonPropertyName("lastClickedAt")]
    public DateTime? LastClickedAt { get; set; }

    /// <summary>When the link was created.</summary>
    [JsonPropertyName("createdAt")]
    public DateTime CreatedAt { get; set; }

    /// <summary>14-day daily click histogram, oldest first (today last).</summary>
    [JsonPropertyName("spark")]
    public List<int> Spark { get; set; } = new();
}

/// <summary>
/// Response from <see cref="LinksResource.ListAsync"/>.
/// </summary>
public class ShortLinkListResponse
{
    /// <summary>The workspace's short links, newest first.</summary>
    [JsonPropertyName("links")]
    public List<ShortLinkListItem> Links { get; set; } = new();

    /// <summary>Total number of short links in the workspace.</summary>
    [JsonPropertyName("total")]
    public int Total { get; set; }
}

/// <summary>
/// Response from enabling or disabling a short link.
/// </summary>
public class ShortLinkDisabledResponse
{
    /// <summary>Short code that was updated.</summary>
    [JsonPropertyName("code")]
    public string Code { get; set; } = string.Empty;

    /// <summary>New disabled state.</summary>
    [JsonPropertyName("disabled")]
    public bool Disabled { get; set; }
}
