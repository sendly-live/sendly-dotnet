using System.Reflection;
using System.Text.Json;
using Sendly.Models;
using Sendly.Tests.Fixtures;
using Xunit;

namespace Sendly.Tests;

/// <summary>
/// Tests for the deprecated members kept for source compatibility with
/// callers written against 3.36.x.
/// </summary>
public class LegacySurfaceCompatibilityTests : IDisposable
{
    private const string PresetTemplateJson = @"{
        ""id"": ""tmpl_preset"",
        ""name"": ""Verification code"",
        ""text"": ""Your {{app_name}} code is {{code}}"",
        ""variables"": [
            { ""key"": ""app_name"", ""type"": ""string"", ""fallback"": ""Acme"" },
            { ""key"": ""code"", ""type"": ""string"", ""fallback"": null }
        ],
        ""is_preset"": true,
        ""preset_slug"": ""otp"",
        ""status"": ""published"",
        ""version"": 2,
        ""published_at"": ""2026-01-02T03:04:05Z"",
        ""created_at"": ""2026-01-01T00:00:00Z"",
        ""updated_at"": ""2026-01-02T03:04:05Z""
    }";

    private const string DraftTemplateJson = @"{
        ""id"": ""tmpl_draft"",
        ""name"": ""Shift reminder"",
        ""text"": ""You are on at {{time}}"",
        ""variables"": [ { ""key"": ""time"", ""type"": ""string"", ""fallback"": null } ],
        ""is_preset"": false,
        ""preset_slug"": null,
        ""status"": ""draft"",
        ""version"": 1,
        ""published_at"": null,
        ""created_at"": ""2026-01-01T00:00:00Z"",
        ""updated_at"": ""2026-01-01T00:00:00Z""
    }";

    private readonly MockHttpMessageHandler _mockHandler;
    private readonly HttpClient _httpClient;
    private readonly SendlyClient _client;

    public LegacySurfaceCompatibilityTests()
    {
        _mockHandler = new MockHttpMessageHandler();
        _httpClient = new HttpClient(_mockHandler)
        {
            BaseAddress = new Uri("https://api.test.com")
        };

        _client = new SendlyClient("test_api_key");
        var httpClientField = typeof(SendlyClient).GetField("_httpClient", BindingFlags.NonPublic | BindingFlags.Instance);
        httpClientField?.SetValue(_client, _httpClient);
    }

    public void Dispose()
    {
        _client?.Dispose();
        _httpClient?.Dispose();
        _mockHandler?.Dispose();
        GC.SuppressFinalize(this);
    }

#pragma warning disable CS0618

    #region Template

    [Fact]
    public async Task GetAsync_PresetTemplate_PopulatesDeprecatedMembers()
    {
        _mockHandler.QueueSuccessResponse(PresetTemplateJson);

        var template = await _client.Templates.GetAsync("tmpl_preset");

        Assert.Equal("preset", template.Type);
        Assert.True(template.IsPreset);
        Assert.False(template.IsCustom);
        Assert.True(template.IsPublished);
        Assert.False(template.IsDefault);
        Assert.Equal(new[] { "app_name", "code" }, template.Variables);
        Assert.Equal("Your {{app_name}} code is {{code}}", template.Body);
        Assert.Equal(2, template.VariableDefinitions.Count);
        Assert.Equal("Acme", template.VariableDefinitions[0].Fallback);
    }

    [Fact]
    public async Task GetAsync_DraftCustomTemplate_PopulatesDeprecatedMembers()
    {
        _mockHandler.QueueSuccessResponse(DraftTemplateJson);

        var template = await _client.Templates.GetAsync("tmpl_draft");

        Assert.Equal("custom", template.Type);
        Assert.True(template.IsCustom);
        Assert.False(template.IsPublished);
        Assert.Equal(new[] { "time" }, template.Variables);
    }

    [Fact]
    public void Template_DeprecatedSettersStayConsistentWithReplacements()
    {
        var template = new Template();

        Assert.Equal("custom", template.Type);
        Assert.Empty(template.Variables);
        Assert.False(template.IsPublished);

        template.Type = "preset";
        Assert.True(template.IsPreset);
        Assert.False(template.IsCustom);

        template.Variables = new List<string> { "first_name" };
        Assert.Equal("first_name", template.VariableDefinitions.Single().Key);

        template.IsPublished = true;
        Assert.True(template.IsPublished);
    }

    [Fact]
    public void Template_DeprecatedMembersAreNotSerialised()
    {
        var template = new Template
        {
            Id = "tmpl_1",
            Name = "Shift reminder",
            Body = "You are on at {{time}}",
            IsPreset = true,
            IsDefault = true,
            VariableDefinitions = new List<TemplateVariable> { new() { Key = "time" } }
        };

        var json = JsonSerializer.Serialize(template, WireOptions());

        Assert.DoesNotContain("\"type\":\"preset\"", json);
        Assert.DoesNotContain("is_default", json);
        Assert.DoesNotContain("is_published", json);
        Assert.Single(CountOccurrences(json, "\"variables\""));
        Assert.Contains("\"text\":\"You are on at {{time}}\"", json);
    }

    [Fact]
    public async Task CreateAsync_DoesNotSendDeprecatedFields()
    {
        _mockHandler.QueueSuccessResponse(DraftTemplateJson);

        await _client.Templates.CreateAsync(new CreateTemplateRequest
        {
            Name = "Shift reminder",
            Body = "You are on at {{time}}",
            Locale = "en-GB",
            IsPublished = true
        });

        var body = await ReadLastRequestBodyAsync();

        Assert.Contains("\"name\":\"Shift reminder\"", body);
        Assert.Contains("\"text\":\"You are on at {{time}}\"", body);
        Assert.DoesNotContain("locale", body);
        Assert.DoesNotContain("published", body);
    }

    [Fact]
    public async Task UpdateAsync_DoesNotSendDeprecatedFields()
    {
        _mockHandler.QueueSuccessResponse(DraftTemplateJson);

        await _client.Templates.UpdateAsync("tmpl_draft", new UpdateTemplateRequest
        {
            Body = "You are on at {{time}} sharp",
            Locale = "en-GB",
            IsPublished = false
        });

        var body = await ReadLastRequestBodyAsync();

        Assert.Contains("\"text\":\"You are on at {{time}} sharp\"", body);
        Assert.DoesNotContain("locale", body);
        Assert.DoesNotContain("published", body);
    }

    #endregion

    #region BatchMessageResponse

    [Fact]
    public async Task SendBatchAsync_DeprecatedQueued_ReadsZeroWhenNotReported()
    {
        _mockHandler.QueueSuccessResponse(@"{
            ""batchId"": ""batch_1"",
            ""status"": ""completed"",
            ""total"": 1,
            ""sent"": 1,
            ""failed"": 0,
            ""creditsUsed"": 1,
            ""messages"": []
        }");

        var response = await _client.Messages.SendBatchAsync(new SendBatchRequest
        {
            Messages = new List<BatchMessageItem> { new("+15551234567", "hi") }
        });

        Assert.Null(response.QueuedCount);
        Assert.Equal(0, response.Queued);
    }

    [Fact]
    public async Task GetBatchAsync_DeprecatedQueued_MirrorsReportedCount()
    {
        _mockHandler.QueueSuccessResponse(@"{
            ""id"": ""batch_2"",
            ""status"": ""processing"",
            ""total"": 100,
            ""queued"": 50,
            ""sent"": 50,
            ""failed"": 0,
            ""creditsUsed"": 50,
            ""messages"": []
        }");

        var response = await _client.Messages.GetBatchAsync("batch_2");

        Assert.Equal(50, response.QueuedCount);
        Assert.Equal(50, response.Queued);
    }

    [Fact]
    public void BatchMessageResponse_DeprecatedQueuedSetterWritesThrough()
    {
        var response = new BatchMessageResponse { Queued = 7 };

        Assert.Equal(7, response.QueuedCount);
        Assert.Equal(7, response.Queued);

        response.QueuedCount = null;
        Assert.Equal(0, response.Queued);
    }

    [Fact]
    public void BatchMessageResponse_DeprecatedQueuedIsNotSerialised()
    {
        var json = JsonSerializer.Serialize(new BatchMessageResponse { Queued = 3 }, WireOptions());

        Assert.Single(CountOccurrences(json, "\"queued\""));
        Assert.Contains("\"queued\":3", json);
    }

    #endregion

#pragma warning restore CS0618

    private async Task<string> ReadLastRequestBodyAsync()
    {
        var request = _mockHandler.LastRequest;
        Assert.NotNull(request);
        Assert.NotNull(request!.Content);
        return await request.Content!.ReadAsStringAsync();
    }

    private static JsonSerializerOptions WireOptions() => new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        PropertyNameCaseInsensitive = true
    };

    private static List<int> CountOccurrences(string haystack, string needle)
    {
        var positions = new List<int>();
        var index = haystack.IndexOf(needle, StringComparison.Ordinal);
        while (index >= 0)
        {
            positions.Add(index);
            index = haystack.IndexOf(needle, index + needle.Length, StringComparison.Ordinal);
        }
        return positions;
    }
}
