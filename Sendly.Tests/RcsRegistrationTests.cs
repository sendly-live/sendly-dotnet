using System.Net;
using System.Reflection;
using System.Text.Json;
using Sendly.Exceptions;
using Sendly.Models;
using Sendly.Resources;
using Sendly.Tests.Fixtures;
using Xunit;

namespace Sendly.Tests;

/// <summary>
/// Tests for RCS registration - Registration.Get, Dossier.Get, Brands
/// Create/Update, and Agents Create/Get/Update/SetTestDevices/Submit/RequestLaunch.
/// </summary>
public class RcsRegistrationTests : IDisposable
{
    private const string AutoKeyPattern =
        @"^sendly-dotnet-retry-[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$";

    private const string BrandJson = @"{
        ""id"": ""rcs_brand_123"",
        ""reviewStatus"": ""draft"",
        ""customerStage"": ""draft"",
        ""displayName"": ""Acme Coffee"",
        ""legalName"": ""Acme Coffee LLC"",
        ""legalEntityType"": ""LIMITED_LIABILITY_COMPANY"",
        ""organizationType"": ""PRIVATE_PROFIT"",
        ""stockSymbol"": null,
        ""websiteUrl"": ""https://acme.example"",
        ""ein"": ""12-3456789"",
        ""address"": {
            ""line1"": ""100 Main St"",
            ""line2"": null,
            ""city"": ""Chicago"",
            ""state"": ""IL"",
            ""postalCode"": ""60601"",
            ""countryCode"": ""US""
        },
        ""contact"": {
            ""firstName"": ""Sam"",
            ""lastName"": ""Lee"",
            ""title"": null,
            ""email"": ""sam@acme.example"",
            ""phoneNumber"": ""+13125550100""
        },
        ""reviewNote"": null,
        ""rejectionReason"": null,
        ""submittedForReviewAt"": null,
        ""sentToCarrierAt"": null,
        ""verifiedAt"": null,
        ""createdAt"": ""2026-09-01T10:00:00Z"",
        ""updatedAt"": ""2026-09-01T10:00:00Z""
    }";

    private const string AgentJson = @"{
        ""id"": ""rcs_agent_123"",
        ""brandId"": ""rcs_brand_123"",
        ""status"": ""draft"",
        ""reviewStatus"": ""draft"",
        ""customerStage"": ""draft"",
        ""displayName"": ""Acme Coffee"",
        ""useCase"": ""MULTI_USE"",
        ""hostingRegion"": null,
        ""basics"": {
            ""displayName"": ""Acme Coffee"",
            ""useCase"": ""MULTI_USE"",
            ""hostingRegion"": null,
            ""description"": ""Order updates and support"",
            ""logoUrl"": ""https://acme.example/rcs/logo.png"",
            ""brandColor"": ""#0B6E4F"",
            ""website"": { ""url"": ""https://acme.example"", ""label"": ""Visit our site"" }
        },
        ""campaign"": {
            ""agentOverview"": ""Order confirmations and support replies"",
            ""interactions"": [{ ""interactionType"": ""TRANSACTIONAL_UPDATES"", ""description"": ""Order status"" }],
            ""messageExamples"": [""Your order is ready."", ""Your order shipped."", ""Reply HELP for support.""],
            ""consentSettings"": {
                ""optInMethods"": [{ ""methodType"": ""WEBSITE"", ""description"": ""Checkout checkbox"" }],
                ""callToAction"": ""Text me order updates"",
                ""doubleOptIn"": false
            }
        },
        ""testing"": { ""testUrl"": ""https://acme.example/rcs-test"", ""messageId"": null, ""additionalInformation"": null },
        ""reviewNote"": null,
        ""rejectionReason"": null,
        ""testDevices"": [
            { ""id"": ""rcs_dev_1"", ""phoneNumber"": ""+13125550100"", ""label"": ""Sam Pixel"", ""inviteStatus"": ""PENDING"", ""createdAt"": ""2026-09-02T10:00:00Z"" }
        ],
        ""submittedForReviewAt"": null,
        ""basicsSubmittedAt"": null,
        ""launchSubmittedAt"": null,
        ""liveAt"": null,
        ""createdAt"": ""2026-09-01T10:00:00Z"",
        ""updatedAt"": ""2026-09-02T10:00:00Z""
    }";

    private const string NotEnabledJson =
        @"{""error"": ""rcs_not_enabled"", ""message"": ""RCS registration isn't enabled for this account yet.""}";

    private readonly MockHttpMessageHandler _mockHandler;
    private readonly HttpClient _httpClient;
    private readonly SendlyClient _client;

    public RcsRegistrationTests()
    {
        _mockHandler = new MockHttpMessageHandler();
        _httpClient = new HttpClient(_mockHandler)
        {
            BaseAddress = new Uri("https://api.test.com")
        };

        _client = new SendlyClient("test_api_key", new SendlyClientOptions { MaxRetries = 0 });
        var httpClientField = typeof(SendlyClient).GetField("_httpClient", BindingFlags.NonPublic | BindingFlags.Instance);
        httpClientField?.SetValue(_client, _httpClient);
    }

    public void Dispose()
    {
        _client?.Dispose();
        _httpClient?.Dispose();
        _mockHandler?.Dispose();
    }

    private string? KeyOfLastRequest()
    {
        return _mockHandler.LastRequest!.Headers.TryGetValues("Idempotency-Key", out var values)
            ? values.FirstOrDefault()
            : null;
    }

    private async Task<string> BodyOfLastRequest()
    {
        return await _mockHandler.LastRequest!.Content!.ReadAsStringAsync();
    }

    private async Task<JsonElement> JsonBodyOfLastRequest()
    {
        using var doc = JsonDocument.Parse(await BodyOfLastRequest());
        return doc.RootElement.Clone();
    }

    #region Registration.GetAsync Tests

    [Fact]
    public async Task RegistrationGetAsync_HitsPathAndMapsResponse()
    {
        _mockHandler.QueueSuccessResponse($@"{{
            ""brand"": {BrandJson},
            ""agent"": {AgentJson},
            ""devices"": [{{ ""id"": ""rcs_dev_1"", ""phoneNumber"": ""+13125550100"", ""label"": ""Sam Pixel"", ""inviteStatus"": ""PENDING"", ""createdAt"": ""2026-09-02T10:00:00Z"" }}],
            ""stage"": ""testing"",
            ""usEligible"": true
        }}");

        var result = await _client.Rcs.Registration.GetAsync();

        var request = _mockHandler.LastRequest!;
        Assert.Equal(HttpMethod.Get, request.Method);
        Assert.EndsWith("rcs/registration", request.RequestUri!.ToString());
        Assert.Null(KeyOfLastRequest());

        Assert.Equal(RcsCustomerStage.Testing, result.Stage);
        Assert.True(result.UsEligible);
        Assert.Equal("rcs_brand_123", result.Brand!.Id);
        Assert.Equal("Chicago", result.Brand.Address.City);
        Assert.Equal("sam@acme.example", result.Brand.Contact.Email);
        Assert.Null(result.Brand.StockSymbol);
        Assert.Equal("rcs_agent_123", result.Agent!.Id);
        Assert.Equal("https://acme.example/rcs/logo.png", result.Agent.Basics.LogoUrl);
        Assert.Equal("Visit our site", result.Agent.Basics.Website!.Label);
        Assert.Single(result.Agent.Campaign!.Interactions!);
        Assert.Equal(RcsInteractionType.TransactionalUpdates, result.Agent.Campaign.Interactions![0].InteractionType);
        Assert.Equal(3, result.Agent.Campaign.MessageExamples!.Count);
        Assert.False(result.Agent.Campaign.ConsentSettings!.DoubleOptIn);
        Assert.Equal("https://acme.example/rcs-test", result.Agent.Testing!.TestUrl);
        Assert.Single(result.Devices);
        Assert.Equal("PENDING", result.Devices[0].InviteStatus);
    }

    [Fact]
    public async Task RegistrationGetAsync_WhenNothingExists_ReturnsNulls()
    {
        _mockHandler.QueueSuccessResponse(@"{""brand"": null, ""agent"": null, ""devices"": [], ""stage"": ""draft"", ""usEligible"": true}");

        var result = await _client.Rcs.Registration.GetAsync();

        Assert.Null(result.Brand);
        Assert.Null(result.Agent);
        Assert.Empty(result.Devices);
        Assert.Equal(RcsCustomerStage.Draft, result.Stage);
    }

    #endregion

    #region Dossier.GetAsync Tests

    [Fact]
    public async Task DossierGetAsync_HitsPathAndMapsBrandInput()
    {
        _mockHandler.QueueSuccessResponse(@"{
            ""brand"": {
                ""legalName"": ""Acme Coffee LLC"",
                ""ein"": ""12-3456789"",
                ""organizationType"": ""PRIVATE_PROFIT"",
                ""address"": { ""line1"": ""100 Main St"", ""city"": ""Chicago"", ""state"": ""IL"", ""postalCode"": ""60601"", ""countryCode"": ""US"" },
                ""contact"": { ""firstName"": ""Sam"", ""lastName"": ""Lee"", ""email"": ""sam@acme.example"" }
            },
            ""usEligible"": true,
            ""source"": ""tendlc""
        }");

        var result = await _client.Rcs.Dossier.GetAsync();

        var request = _mockHandler.LastRequest!;
        Assert.Equal(HttpMethod.Get, request.Method);
        Assert.EndsWith("rcs/dossier", request.RequestUri!.ToString());

        Assert.Equal(RcsDossierSource.TenDlc, result.Source);
        Assert.True(result.UsEligible);
        Assert.Equal("Acme Coffee LLC", result.Brand.LegalName);
        Assert.Equal("12-3456789", result.Brand.Ein);
        Assert.Null(result.Brand.DisplayName);
        Assert.Equal("US", result.Brand.Address!.CountryCode);
        Assert.Equal("Sam", result.Brand.Contact!.FirstName);
        Assert.Null(result.Brand.Contact.Title);
    }

    [Fact]
    public async Task DossierGetAsync_WhenNothingOnFile_ReturnsEmptyBrand()
    {
        _mockHandler.QueueSuccessResponse(@"{""brand"": {}, ""usEligible"": true, ""source"": ""none""}");

        var result = await _client.Rcs.Dossier.GetAsync();

        Assert.Equal(RcsDossierSource.None, result.Source);
        Assert.Null(result.Brand.LegalName);
        Assert.Null(result.Brand.Address);
    }

    #endregion

    #region Brands.CreateAsync Tests

    [Fact]
    public async Task BrandsCreateAsync_SendsNestedBodyAndMapsBrand()
    {
        _mockHandler.QueueResponse(HttpStatusCode.Created, $@"{{""brand"": {BrandJson}}}");

        var result = await _client.Rcs.Brands.CreateAsync(new RcsBrandInput
        {
            DisplayName = "Acme Coffee",
            LegalName = "Acme Coffee LLC",
            LegalEntityType = RcsLegalEntityType.LimitedLiabilityCompany,
            OrganizationType = RcsOrganizationType.PrivateProfit,
            WebsiteUrl = "https://acme.example",
            Ein = "12-3456789",
            Address = new RcsBrandAddressInput
            {
                Line1 = "100 Main St",
                City = "Chicago",
                State = "IL",
                PostalCode = "60601",
                CountryCode = "US"
            },
            Contact = new RcsBrandContactInput
            {
                FirstName = "Sam",
                LastName = "Lee",
                Email = "sam@acme.example",
                PhoneNumber = "+13125550100"
            }
        });

        var request = _mockHandler.LastRequest!;
        Assert.Equal(HttpMethod.Post, request.Method);
        Assert.EndsWith("rcs/brands", request.RequestUri!.ToString());
        var body = await JsonBodyOfLastRequest();
        Assert.Equal("Acme Coffee", body.GetProperty("displayName").GetString());
        Assert.Equal("LIMITED_LIABILITY_COMPANY", body.GetProperty("legalEntityType").GetString());
        Assert.Equal("100 Main St", body.GetProperty("address").GetProperty("line1").GetString());
        Assert.Equal("US", body.GetProperty("address").GetProperty("countryCode").GetString());
        Assert.False(body.GetProperty("address").TryGetProperty("line2", out _));
        Assert.Equal("Sam", body.GetProperty("contact").GetProperty("firstName").GetString());
        Assert.Equal("+13125550100", body.GetProperty("contact").GetProperty("phoneNumber").GetString());
        Assert.False(body.GetProperty("contact").TryGetProperty("title", out _));
        Assert.False(body.TryGetProperty("stockSymbol", out _));
        Assert.Matches(AutoKeyPattern, KeyOfLastRequest());

        Assert.Equal("rcs_brand_123", result.Brand.Id);
        Assert.Equal(RcsReviewStatus.Draft, result.Brand.ReviewStatus);
        Assert.Equal(RcsCustomerStage.Draft, result.Brand.CustomerStage);
        Assert.Equal("Acme Coffee LLC", result.Brand.LegalName);
        Assert.Equal("60601", result.Brand.Address.PostalCode);
        Assert.Equal("+13125550100", result.Brand.Contact.PhoneNumber);
        Assert.Null(result.Brand.SubmittedForReviewAt);
    }

    [Fact]
    public async Task BrandsCreateAsync_WithCallerKey_SendsKeyVerbatim()
    {
        _mockHandler.QueueResponse(HttpStatusCode.Created, $@"{{""brand"": {BrandJson}}}");

        await _client.Rcs.Brands.CreateAsync(
            new RcsBrandInput { DisplayName = "Acme Coffee" },
            new IdempotentRequestOptions { IdempotencyKey = "rcs-brand-acme" });

        Assert.Equal("rcs-brand-acme", KeyOfLastRequest());
    }

    [Fact]
    public async Task BrandsCreateAsync_OutsideUs_ThrowsValidationExceptionWithApiErrorCode()
    {
        _mockHandler.QueueResponse(HttpStatusCode.UnprocessableEntity,
            @"{""error"": ""rcs_us_only"", ""message"": ""RCS registration is available to US businesses for now.""}");

        var exception = await Assert.ThrowsAsync<ValidationException>(
            () => _client.Rcs.Brands.CreateAsync(new RcsBrandInput
            {
                Address = new RcsBrandAddressInput { CountryCode = "GB" }
            }));

        Assert.Equal(RcsErrorCode.UsOnly, exception.ApiErrorCode);
        Assert.Contains("US businesses", exception.Message);
        Assert.Empty(exception.FieldErrors);
    }

    #endregion

    #region Brands.UpdateAsync Tests

    [Fact]
    public async Task BrandsUpdateAsync_SendsPatchWithOnlySetFields()
    {
        _mockHandler.QueueSuccessResponse($@"{{""brand"": {BrandJson}}}");

        var result = await _client.Rcs.Brands.UpdateAsync("rcs_brand_123", new RcsBrandInput
        {
            WebsiteUrl = "https://acme.example",
            Contact = new RcsBrandContactInput { Title = "Head of Support" }
        });

        var request = _mockHandler.LastRequest!;
        Assert.Equal(HttpMethod.Patch, request.Method);
        Assert.EndsWith("rcs/brands/rcs_brand_123", request.RequestUri!.ToString());
        Assert.Equal("{\"websiteUrl\":\"https://acme.example\",\"contact\":{\"title\":\"Head of Support\"}}", await BodyOfLastRequest());
        Assert.Null(KeyOfLastRequest());
        Assert.Equal("rcs_brand_123", result.Brand.Id);
    }

    [Fact]
    public async Task BrandsUpdateAsync_WithCallerKey_SendsKey()
    {
        _mockHandler.QueueSuccessResponse($@"{{""brand"": {BrandJson}}}");

        await _client.Rcs.Brands.UpdateAsync("rcs_brand_123",
            new RcsBrandInput { Ein = "12-3456789" },
            new IdempotentRequestOptions { IdempotencyKey = "rcs-brand-ein-fix" });

        Assert.Equal(HttpMethod.Patch, _mockHandler.LastRequest!.Method);
        Assert.Equal("rcs-brand-ein-fix", KeyOfLastRequest());
    }

    [Fact]
    public async Task BrandsUpdateAsync_WhenLocked_ThrowsSendlyExceptionWith409()
    {
        _mockHandler.QueueResponse(HttpStatusCode.Conflict,
            @"{""error"": ""rcs_field_locked"", ""message"": ""This registration is being reviewed; we will email you if changes are needed.""}");

        var exception = await Assert.ThrowsAsync<SendlyException>(
            () => _client.Rcs.Brands.UpdateAsync("rcs_brand_123", new RcsBrandInput { Ein = "12-3456789" }));

        Assert.Equal(409, exception.StatusCode);
        Assert.Equal(RcsErrorCode.FieldLocked, exception.ApiErrorCode);
        Assert.Contains("being reviewed", exception.Message);
    }

    [Fact]
    public async Task BrandsUpdateAsync_WhenUnknown_ThrowsNotFoundException()
    {
        _mockHandler.QueueResponse(HttpStatusCode.NotFound,
            @"{""error"": ""rcs_not_found"", ""message"": ""No brand with that id.""}");

        var exception = await Assert.ThrowsAsync<NotFoundException>(
            () => _client.Rcs.Brands.UpdateAsync("rcs_brand_nope", new RcsBrandInput { Ein = "12-3456789" }));

        Assert.Equal(RcsErrorCode.NotFound, exception.ApiErrorCode);
    }

    #endregion

    #region Agents.CreateAsync Tests

    [Fact]
    public async Task AgentsCreateAsync_SendsBodyAndMapsAgent()
    {
        _mockHandler.QueueResponse(HttpStatusCode.Created, $@"{{""agent"": {AgentJson}}}");

        var result = await _client.Rcs.Agents.CreateAsync(new CreateRcsAgentRequest
        {
            BrandId = "rcs_brand_123",
            DisplayName = "Acme Coffee",
            UseCase = RcsAgentUseCase.MultiUse,
            Basics = new RcsAgentBasicsInput
            {
                Description = "Order updates and support",
                LogoUrl = "https://acme.example/rcs/logo.png",
                BrandColor = "#0B6E4F",
                Website = new RcsAgentWebsiteContact { Url = "https://acme.example", Label = "Visit our site" }
            }
        });

        var request = _mockHandler.LastRequest!;
        Assert.Equal(HttpMethod.Post, request.Method);
        Assert.EndsWith("rcs/agents", request.RequestUri!.ToString());
        var body = await BodyOfLastRequest();
        Assert.Contains("\"brandId\":\"rcs_brand_123\"", body);
        Assert.Contains("\"displayName\":\"Acme Coffee\"", body);
        Assert.Contains("\"useCase\":\"MULTI_USE\"", body);
        Assert.Contains("\"basics\":{\"description\":\"Order updates and support\",\"logoUrl\":\"https://acme.example/rcs/logo.png\",\"brandColor\":\"#0B6E4F\",\"website\":{\"url\":\"https://acme.example\",\"label\":\"Visit our site\"}}", body);
        Assert.DoesNotContain("campaign", body);
        Assert.DoesNotContain("testing", body);
        Assert.DoesNotContain("heroUrl", body);
        Assert.Matches(AutoKeyPattern, KeyOfLastRequest());

        Assert.Equal("rcs_agent_123", result.Agent.Id);
        Assert.Equal("rcs_brand_123", result.Agent.BrandId);
        Assert.Equal("draft", result.Agent.Status);
        Assert.Equal(RcsReviewStatus.Draft, result.Agent.ReviewStatus);
        Assert.Equal(RcsAgentUseCase.MultiUse, result.Agent.UseCase);
        Assert.Null(result.Agent.HostingRegion);
        Assert.Equal("#0B6E4F", result.Agent.Basics.BrandColor);
        Assert.Single(result.Agent.TestDevices);
        Assert.Equal("Sam Pixel", result.Agent.TestDevices[0].Label);
    }

    [Fact]
    public async Task AgentsCreateAsync_WithoutBrandId_ThrowsValidationExceptionWithoutRequest()
    {
        await Assert.ThrowsAsync<ValidationException>(
            () => _client.Rcs.Agents.CreateAsync(new CreateRcsAgentRequest { DisplayName = "Acme Coffee" }));

        Assert.Empty(_mockHandler.Requests);
    }

    [Fact]
    public async Task AgentsCreateAsync_WithNonHttpsLogo_ThrowsValidationExceptionWithFieldErrors()
    {
        _mockHandler.QueueResponse(HttpStatusCode.UnprocessableEntity, @"{
            ""error"": ""rcs_invalid_content"",
            ""message"": ""Assets can't be uploaded over the API. Logo, hero, and call-to-action media must be public https:// URLs."",
            ""errors"": [{ ""path"": ""basics.logoUrl"", ""message"": ""Must be a public https:// URL"" }]
        }");

        var exception = await Assert.ThrowsAsync<ValidationException>(
            () => _client.Rcs.Agents.CreateAsync(new CreateRcsAgentRequest
            {
                BrandId = "rcs_brand_123",
                Basics = new RcsAgentBasicsInput { LogoUrl = "data:image/png;base64,AAAA" }
            }));

        Assert.Equal(RcsErrorCode.InvalidContent, exception.ApiErrorCode);
        Assert.Single(exception.FieldErrors);
        Assert.Equal("basics.logoUrl", exception.FieldErrors[0].Path);
        Assert.Equal("Must be a public https:// URL", exception.FieldErrors[0].Message);
    }

    #endregion

    #region Agents.GetAsync Tests

    [Fact]
    public async Task AgentsGetAsync_HitsPathAndMapsDetail()
    {
        _mockHandler.QueueSuccessResponse($@"{{
            ""agent"": {AgentJson},
            ""devices"": [{{ ""id"": ""rcs_dev_1"", ""phoneNumber"": ""+13125550100"", ""label"": ""Sam Pixel"", ""inviteStatus"": ""PENDING"", ""createdAt"": ""2026-09-02T10:00:00Z"" }}],
            ""stage"": ""testing""
        }}");

        var result = await _client.Rcs.Agents.GetAsync("rcs_agent_123");

        var request = _mockHandler.LastRequest!;
        Assert.Equal(HttpMethod.Get, request.Method);
        Assert.EndsWith("rcs/agents/rcs_agent_123", request.RequestUri!.ToString());

        Assert.Equal(RcsCustomerStage.Testing, result.Stage);
        Assert.Equal("rcs_agent_123", result.Agent.Id);
        Assert.Equal("Order confirmations and support replies", result.Agent.Campaign!.AgentOverview);
        Assert.Equal(RcsOptInMethodType.Website, result.Agent.Campaign.ConsentSettings!.OptInMethods![0].MethodType);
        Assert.Single(result.Devices);
        Assert.Equal("+13125550100", result.Devices[0].PhoneNumber);
    }

    [Fact]
    public async Task AgentsGetAsync_WhenUnknown_ThrowsNotFoundException()
    {
        _mockHandler.QueueResponse(HttpStatusCode.NotFound,
            @"{""error"": ""rcs_not_found"", ""message"": ""No agent with that id.""}");

        var exception = await Assert.ThrowsAsync<NotFoundException>(() => _client.Rcs.Agents.GetAsync("rcs_agent_nope"));

        Assert.Equal(RcsErrorCode.NotFound, exception.ApiErrorCode);
    }

    #endregion

    #region Agents.UpdateAsync Tests

    [Fact]
    public async Task AgentsUpdateAsync_SendsCampaignPatch()
    {
        _mockHandler.QueueSuccessResponse($@"{{""agent"": {AgentJson}}}");

        var result = await _client.Rcs.Agents.UpdateAsync("rcs_agent_123", new UpdateRcsAgentRequest
        {
            Campaign = new RcsCampaign
            {
                AgentOverview = "Order confirmations and support replies",
                Interactions = new() { new RcsInteraction { InteractionType = RcsInteractionType.TransactionalUpdates, Description = "Order status" } },
                MessageExamples = new() { "Your order is ready.", "Your order shipped.", "Reply HELP for support." },
                ConsentSettings = new RcsConsentSettings
                {
                    OptInMethods = new() { new RcsOptInMethod { MethodType = RcsOptInMethodType.Website, Description = "Checkout checkbox" } },
                    CallToAction = "Text me order updates",
                    DoubleOptIn = false
                }
            }
        });

        var request = _mockHandler.LastRequest!;
        Assert.Equal(HttpMethod.Patch, request.Method);
        Assert.EndsWith("rcs/agents/rcs_agent_123", request.RequestUri!.ToString());
        var body = await BodyOfLastRequest();
        Assert.StartsWith("{\"campaign\":{\"agentOverview\":\"Order confirmations and support replies\",\"interactions\":[{\"interactionType\":\"TRANSACTIONAL_UPDATES\",\"description\":\"Order status\"}],\"messageExamples\":[", body);
        Assert.Contains("\"consentSettings\":{\"optInMethods\":[{\"methodType\":\"WEBSITE\",\"description\":\"Checkout checkbox\"}],\"callToAction\":\"Text me order updates\",\"doubleOptIn\":false}", body);
        Assert.DoesNotContain("basics", body);
        Assert.DoesNotContain("testing", body);
        Assert.Null(KeyOfLastRequest());
        Assert.Equal("rcs_agent_123", result.Agent.Id);
    }

    [Fact]
    public async Task AgentsUpdateAsync_ClearSections_SendsNulls()
    {
        _mockHandler.QueueSuccessResponse($@"{{""agent"": {AgentJson}}}");

        await _client.Rcs.Agents.UpdateAsync("rcs_agent_123", new UpdateRcsAgentRequest
        {
            DisplayName = "Acme Coffee",
            ClearCampaign = true,
            ClearTesting = true
        });

        Assert.Equal("{\"displayName\":\"Acme Coffee\",\"campaign\":null,\"testing\":null}", await BodyOfLastRequest());
    }

    [Fact]
    public async Task AgentsUpdateAsync_WithCallerKey_SendsKey()
    {
        _mockHandler.QueueSuccessResponse($@"{{""agent"": {AgentJson}}}");

        await _client.Rcs.Agents.UpdateAsync("rcs_agent_123",
            new UpdateRcsAgentRequest { Testing = new RcsTesting { TestUrl = "https://acme.example/rcs-test" } },
            new IdempotentRequestOptions { IdempotencyKey = "rcs-agent-testing-1" });

        Assert.Equal("{\"testing\":{\"testUrl\":\"https://acme.example/rcs-test\"}}", await BodyOfLastRequest());
        Assert.Equal("rcs-agent-testing-1", KeyOfLastRequest());
    }

    [Fact]
    public async Task AgentsUpdateAsync_WhenLocked_ThrowsSendlyExceptionWith409()
    {
        _mockHandler.QueueResponse(HttpStatusCode.Conflict,
            @"{""error"": ""rcs_field_locked"", ""message"": ""This registration is being reviewed; we will email you if changes are needed.""}");

        var exception = await Assert.ThrowsAsync<SendlyException>(
            () => _client.Rcs.Agents.UpdateAsync("rcs_agent_123", new UpdateRcsAgentRequest { DisplayName = "Acme" }));

        Assert.Equal(409, exception.StatusCode);
        Assert.Equal(RcsErrorCode.FieldLocked, exception.ApiErrorCode);
    }

    #endregion

    #region Agents.SetTestDevicesAsync Tests

    [Fact]
    public async Task AgentsSetTestDevicesAsync_SendsPutAndMapsDevices()
    {
        _mockHandler.QueueSuccessResponse(@"{""devices"": [
            { ""id"": ""rcs_dev_1"", ""phoneNumber"": ""+13125550100"", ""label"": ""Sam Pixel"", ""inviteStatus"": null, ""createdAt"": ""2026-09-02T10:00:00Z"" },
            { ""id"": ""rcs_dev_2"", ""phoneNumber"": ""+13125550101"", ""label"": null, ""inviteStatus"": null, ""createdAt"": ""2026-09-02T10:00:01Z"" }
        ]}");

        var result = await _client.Rcs.Agents.SetTestDevicesAsync("rcs_agent_123", new[]
        {
            new RcsTestDeviceInput("+13125550100", "Sam Pixel"),
            new RcsTestDeviceInput { PhoneNumber = "+13125550101" }
        });

        var request = _mockHandler.LastRequest!;
        Assert.Equal(HttpMethod.Put, request.Method);
        Assert.EndsWith("rcs/agents/rcs_agent_123/test-devices", request.RequestUri!.ToString());
        var body = await JsonBodyOfLastRequest();
        var devices = body.GetProperty("devices");
        Assert.Single(body.EnumerateObject());
        Assert.Equal(2, devices.GetArrayLength());
        Assert.Equal("+13125550100", devices[0].GetProperty("phoneNumber").GetString());
        Assert.Equal("Sam Pixel", devices[0].GetProperty("label").GetString());
        Assert.Equal("+13125550101", devices[1].GetProperty("phoneNumber").GetString());
        Assert.False(devices[1].TryGetProperty("label", out _));
        Assert.Null(KeyOfLastRequest());

        Assert.Equal(2, result.Devices.Count);
        Assert.Equal("rcs_dev_2", result.Devices[1].Id);
        Assert.Null(result.Devices[1].Label);
        Assert.Null(result.Devices[1].InviteStatus);
    }

    [Fact]
    public async Task AgentsSetTestDevicesAsync_WithEmptyList_SendsEmptyDevices()
    {
        _mockHandler.QueueSuccessResponse(@"{""devices"": []}");

        var result = await _client.Rcs.Agents.SetTestDevicesAsync("rcs_agent_123", Array.Empty<RcsTestDeviceInput>());

        Assert.Equal("{\"devices\":[]}", await BodyOfLastRequest());
        Assert.Empty(result.Devices);
    }

    [Fact]
    public async Task AgentsSetTestDevicesAsync_WithCallerKey_SendsKey()
    {
        _mockHandler.QueueSuccessResponse(@"{""devices"": []}");

        await _client.Rcs.Agents.SetTestDevicesAsync("rcs_agent_123",
            new[] { new RcsTestDeviceInput("+13125550100") },
            new IdempotentRequestOptions { IdempotencyKey = "rcs-devices-v3" });

        Assert.Equal(HttpMethod.Put, _mockHandler.LastRequest!.Method);
        Assert.Equal("rcs-devices-v3", KeyOfLastRequest());
    }

    [Fact]
    public async Task AgentsSetTestDevicesAsync_WithEmptyPhone_ThrowsValidationExceptionWithoutRequest()
    {
        await Assert.ThrowsAsync<ValidationException>(
            () => _client.Rcs.Agents.SetTestDevicesAsync("rcs_agent_123", new[] { new RcsTestDeviceInput { Label = "No number" } }));

        Assert.Empty(_mockHandler.Requests);
    }

    [Fact]
    public async Task AgentsSetTestDevicesAsync_InvalidNumber_ThrowsValidationExceptionWithFieldErrors()
    {
        _mockHandler.QueueResponse(HttpStatusCode.UnprocessableEntity, @"{
            ""error"": ""rcs_invalid_content"",
            ""message"": ""Check the device list."",
            ""errors"": [{ ""path"": ""devices.1.phoneNumber"", ""message"": ""Enter the device's phone number in E.164 format, like +13125550100"" }]
        }");

        var exception = await Assert.ThrowsAsync<ValidationException>(
            () => _client.Rcs.Agents.SetTestDevicesAsync("rcs_agent_123", new[]
            {
                new RcsTestDeviceInput("+13125550100"),
                new RcsTestDeviceInput("not-a-number")
            }));

        Assert.Equal(RcsErrorCode.InvalidContent, exception.ApiErrorCode);
        Assert.Equal("devices.1.phoneNumber", exception.FieldErrors[0].Path);
        Assert.Contains("E.164", exception.FieldErrors[0].Message);
    }

    #endregion

    #region Agents.SubmitAsync Tests

    [Fact]
    public async Task AgentsSubmitAsync_PostsEmptyBodyAndMapsStage()
    {
        _mockHandler.QueueSuccessResponse($@"{{
            ""agent"": {AgentJson.Replace(@"""reviewStatus"": ""draft""", @"""reviewStatus"": ""awaiting_review""")},
            ""stage"": ""in_review""
        }}");

        var result = await _client.Rcs.Agents.SubmitAsync("rcs_agent_123");

        var request = _mockHandler.LastRequest!;
        Assert.Equal(HttpMethod.Post, request.Method);
        Assert.EndsWith("rcs/agents/rcs_agent_123/submit", request.RequestUri!.ToString());
        Assert.Equal("{}", await BodyOfLastRequest());
        Assert.Matches(AutoKeyPattern, KeyOfLastRequest());

        Assert.Equal(RcsCustomerStage.InReview, result.Stage);
        Assert.Equal(RcsReviewStatus.AwaitingReview, result.Agent.ReviewStatus);
    }

    [Fact]
    public async Task AgentsSubmitAsync_WithCallerKey_SendsKey()
    {
        _mockHandler.QueueSuccessResponse($@"{{""agent"": {AgentJson}, ""stage"": ""in_review""}}");

        await _client.Rcs.Agents.SubmitAsync("rcs_agent_123",
            new IdempotentRequestOptions { IdempotencyKey = "rcs-submit-rcs_agent_123" });

        Assert.Equal("rcs-submit-rcs_agent_123", KeyOfLastRequest());
    }

    [Fact]
    public async Task AgentsSubmitAsync_WhenIncomplete_ThrowsValidationExceptionWithFieldErrors()
    {
        _mockHandler.QueueResponse(HttpStatusCode.UnprocessableEntity, @"{
            ""error"": ""rcs_invalid_content"",
            ""message"": ""Finish the brand and agent details first."",
            ""errors"": [
                { ""path"": ""brand.ein"", ""message"": ""Enter a 9-digit EIN"" },
                { ""path"": ""agent.logoUrl"", ""message"": ""Must be a public https:// URL"" }
            ]
        }");

        var exception = await Assert.ThrowsAsync<ValidationException>(() => _client.Rcs.Agents.SubmitAsync("rcs_agent_123"));

        Assert.Equal(RcsErrorCode.InvalidContent, exception.ApiErrorCode);
        Assert.Equal(2, exception.FieldErrors.Count);
        Assert.Equal("brand.ein", exception.FieldErrors[0].Path);
        Assert.Equal("agent.logoUrl: Must be a public https:// URL", exception.FieldErrors[1].ToString());
    }

    [Fact]
    public async Task AgentsSubmitAsync_WhenBrandNotVerified_ThrowsSendlyExceptionWith409()
    {
        _mockHandler.QueueResponse(HttpStatusCode.Conflict,
            @"{""error"": ""rcs_brand_not_verified"", ""message"": ""The brand could not be verified.""}");

        var exception = await Assert.ThrowsAsync<SendlyException>(() => _client.Rcs.Agents.SubmitAsync("rcs_agent_123"));

        Assert.Equal(409, exception.StatusCode);
        Assert.Equal(RcsErrorCode.BrandNotVerified, exception.ApiErrorCode);
    }

    #endregion

    #region Agents.RequestLaunchAsync Tests

    [Fact]
    public async Task AgentsRequestLaunchAsync_PostsTestUrlAndMapsStage()
    {
        _mockHandler.QueueSuccessResponse($@"{{
            ""agent"": {AgentJson.Replace(@"""reviewStatus"": ""draft""", @"""reviewStatus"": ""launch_requested""")},
            ""stage"": ""launch_review""
        }}");

        var result = await _client.Rcs.Agents.RequestLaunchAsync("rcs_agent_123",
            new RcsRequestLaunchRequest { TestUrl = "https://acme.example/rcs-test" });

        var request = _mockHandler.LastRequest!;
        Assert.Equal(HttpMethod.Post, request.Method);
        Assert.EndsWith("rcs/agents/rcs_agent_123/request-launch", request.RequestUri!.ToString());
        Assert.Equal("{\"testUrl\":\"https://acme.example/rcs-test\"}", await BodyOfLastRequest());
        Assert.Matches(AutoKeyPattern, KeyOfLastRequest());

        Assert.Equal(RcsCustomerStage.LaunchReview, result.Stage);
        Assert.Equal(RcsReviewStatus.LaunchRequested, result.Agent.ReviewStatus);
    }

    [Fact]
    public async Task AgentsRequestLaunchAsync_WithoutRequest_PostsEmptyBody()
    {
        _mockHandler.QueueSuccessResponse($@"{{""agent"": {AgentJson}, ""stage"": ""launch_review""}}");

        await _client.Rcs.Agents.RequestLaunchAsync("rcs_agent_123",
            options: new IdempotentRequestOptions { IdempotencyKey = "rcs-launch-rcs_agent_123" });

        Assert.Equal("{}", await BodyOfLastRequest());
        Assert.Equal("rcs-launch-rcs_agent_123", KeyOfLastRequest());
    }

    [Fact]
    public async Task AgentsRequestLaunchAsync_WhenNotReady_ThrowsSendlyExceptionWith409()
    {
        _mockHandler.QueueResponse(HttpStatusCode.Conflict,
            @"{""error"": ""rcs_launch_not_ready"", ""message"": ""This agent isn't ready to launch yet. Finish testing on an invited device first.""}");

        var exception = await Assert.ThrowsAsync<SendlyException>(() => _client.Rcs.Agents.RequestLaunchAsync("rcs_agent_123"));

        Assert.Equal(409, exception.StatusCode);
        Assert.Equal(RcsErrorCode.LaunchNotReady, exception.ApiErrorCode);
        Assert.Contains("invited device", exception.Message);
    }

    #endregion

    #region Agents.ListAsync stage Tests

    [Fact]
    public async Task AgentsListAsync_MapsStage()
    {
        _mockHandler.QueueSuccessResponse(@"{""agents"": [
            { ""id"": ""rcs_agent_123"", ""name"": ""Acme Coffee"", ""status"": ""testing"", ""useCase"": ""MULTI_USE"", ""sendable"": true, ""stage"": ""testing"", ""createdAt"": ""2026-09-01T10:00:00Z"" }
        ]}");

        var result = await _client.Rcs.Agents.ListAsync();

        Assert.Equal(RcsCustomerStage.Testing, result.Agents[0].Stage);
        Assert.True(result.Agents[0].Sendable);
    }

    #endregion

    #region Dark-flag and id guard Tests

    public static IEnumerable<object[]> RegistrationOperations()
    {
        yield return new object[] { "registration.get" };
        yield return new object[] { "dossier.get" };
        yield return new object[] { "brands.create" };
        yield return new object[] { "brands.update" };
        yield return new object[] { "agents.create" };
        yield return new object[] { "agents.get" };
        yield return new object[] { "agents.update" };
        yield return new object[] { "agents.setTestDevices" };
        yield return new object[] { "agents.submit" };
        yield return new object[] { "agents.requestLaunch" };
    }

    private Task Invoke(string operation)
    {
        return operation switch
        {
            "registration.get" => _client.Rcs.Registration.GetAsync(),
            "dossier.get" => _client.Rcs.Dossier.GetAsync(),
            "brands.create" => _client.Rcs.Brands.CreateAsync(new RcsBrandInput { DisplayName = "Acme" }),
            "brands.update" => _client.Rcs.Brands.UpdateAsync("rcs_brand_123", new RcsBrandInput { DisplayName = "Acme" }),
            "agents.create" => _client.Rcs.Agents.CreateAsync(new CreateRcsAgentRequest { BrandId = "rcs_brand_123" }),
            "agents.get" => _client.Rcs.Agents.GetAsync("rcs_agent_123"),
            "agents.update" => _client.Rcs.Agents.UpdateAsync("rcs_agent_123", new UpdateRcsAgentRequest { DisplayName = "Acme" }),
            "agents.setTestDevices" => _client.Rcs.Agents.SetTestDevicesAsync("rcs_agent_123", new[] { new RcsTestDeviceInput("+13125550100") }),
            "agents.submit" => _client.Rcs.Agents.SubmitAsync("rcs_agent_123"),
            "agents.requestLaunch" => _client.Rcs.Agents.RequestLaunchAsync("rcs_agent_123"),
            _ => throw new ArgumentOutOfRangeException(nameof(operation))
        };
    }

    [Theory]
    [MemberData(nameof(RegistrationOperations))]
    public async Task EveryRegistrationOperation_WhenChannelDark_ThrowsNotFoundException(string operation)
    {
        _mockHandler.QueueResponse(HttpStatusCode.NotFound, NotEnabledJson);

        var exception = await Assert.ThrowsAsync<NotFoundException>(() => Invoke(operation));

        Assert.Equal(RcsErrorCode.NotEnabled, exception.ApiErrorCode);
        Assert.Equal("RCS registration isn't enabled for this account yet.", exception.Message);
        Assert.Equal(404, exception.StatusCode);
        Assert.Single(_mockHandler.Requests);
    }

    [Theory]
    [MemberData(nameof(RegistrationOperations))]
    public async Task EveryRegistrationOperation_WhenScopeMissing_ThrowsSendlyExceptionWith403(string operation)
    {
        _mockHandler.QueueResponse(HttpStatusCode.Forbidden,
            @"{""error"": ""insufficient_permissions"", ""message"": ""This API key lacks the rcs:write scope.""}");

        var exception = await Assert.ThrowsAsync<SendlyException>(() => Invoke(operation));

        Assert.Equal(403, exception.StatusCode);
        Assert.Equal(RcsErrorCode.InsufficientPermissions, exception.ApiErrorCode);
    }

    [Fact]
    public async Task IdTakingOperations_WithEmptyId_ThrowValidationExceptionWithoutRequest()
    {
        await Assert.ThrowsAsync<ValidationException>(() => _client.Rcs.Brands.UpdateAsync("", new RcsBrandInput()));
        await Assert.ThrowsAsync<ValidationException>(() => _client.Rcs.Agents.GetAsync(""));
        await Assert.ThrowsAsync<ValidationException>(() => _client.Rcs.Agents.UpdateAsync("", new UpdateRcsAgentRequest()));
        await Assert.ThrowsAsync<ValidationException>(() => _client.Rcs.Agents.SetTestDevicesAsync("", Array.Empty<RcsTestDeviceInput>()));
        await Assert.ThrowsAsync<ValidationException>(() => _client.Rcs.Agents.SubmitAsync(""));
        await Assert.ThrowsAsync<ValidationException>(() => _client.Rcs.Agents.RequestLaunchAsync(""));

        Assert.Empty(_mockHandler.Requests);
    }

    [Fact]
    public async Task IdTakingOperations_EscapeIdsInPaths()
    {
        _mockHandler.QueueSuccessResponse($@"{{""agent"": {AgentJson}, ""devices"": [], ""stage"": ""draft""}}");

        await _client.Rcs.Agents.GetAsync("agent/with space");

        Assert.EndsWith("rcs/agents/agent%2Fwith%20space", _mockHandler.LastRequest!.RequestUri!.AbsoluteUri);
    }

    #endregion
}
