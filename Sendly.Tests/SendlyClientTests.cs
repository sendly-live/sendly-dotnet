using System.Net;
using System.Reflection;
using Sendly.Exceptions;
using Sendly.Tests.Fixtures;
using Xunit;

namespace Sendly.Tests;

/// <summary>
/// Tests for SendlyClient initialization and configuration.
/// </summary>
public class SendlyClientTests
{
    [Fact]
    public void Constructor_WithValidApiKey_InitializesClient()
    {
        // Arrange & Act
        using var client = new SendlyClient("test_api_key");

        // Assert
        Assert.NotNull(client);
        Assert.NotNull(client.Messages);
    }

    [Fact]
    public void Constructor_WithValidApiKeyAndOptions_InitializesClient()
    {
        // Arrange
        var options = new SendlyClientOptions
        {
            BaseUrl = "https://custom.api.com",
            Timeout = TimeSpan.FromSeconds(60),
            MaxRetries = 5
        };

        // Act
        using var client = new SendlyClient("test_api_key", options);

        // Assert
        Assert.NotNull(client);
        Assert.NotNull(client.Messages);
    }

    [Fact]
    public void Constructor_WithNullApiKey_ThrowsAuthenticationException()
    {
        // Act & Assert
        var exception = Assert.Throws<AuthenticationException>(() => new SendlyClient(null!));
        Assert.Equal("API key is required", exception.Message);
        Assert.Equal(401, exception.StatusCode);
    }

    [Fact]
    public void Constructor_WithEmptyApiKey_ThrowsAuthenticationException()
    {
        // Act & Assert
        var exception = Assert.Throws<AuthenticationException>(() => new SendlyClient(""));
        Assert.Equal("API key is required", exception.Message);
        Assert.Equal(401, exception.StatusCode);
    }

    [Fact]
    public void Constructor_WithWhitespaceApiKey_ThrowsAuthenticationException()
    {
        // Act & Assert
        var exception = Assert.Throws<AuthenticationException>(() => new SendlyClient("   "));
        Assert.Equal("API key is required", exception.Message);
    }

    [Fact]
    public void Constructor_WithNullOptions_UsesDefaults()
    {
        // Act
        using var client = new SendlyClient("test_api_key", null);

        // Assert
        Assert.NotNull(client);
        Assert.NotNull(client.Messages);
    }

    [Fact]
    public void DefaultBaseUrl_IsCorrect()
    {
        // Assert
        Assert.Equal("https://sendly.live/api/v1", SendlyClient.DefaultBaseUrl);
    }

    /// <summary>
    /// Reads the HttpClient the SDK built for itself.
    /// </summary>
    private static Uri BaseAddressOf(SendlyClient client)
    {
        var field = typeof(SendlyClient).GetField("_httpClient",
            BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.NotNull(field);
        var http = (HttpClient)field!.GetValue(client)!;
        Assert.NotNull(http.BaseAddress);
        return http.BaseAddress!;
    }

    [Fact]
    public void Constructor_WithDefaultBaseUrl_KeepsVersionedSegmentWhenResolvingPaths()
    {
        // Request paths are relative, and RFC 3986 drops the last segment of a
        // base that does not end in "/" — so an unslashed ".../api/v1" would
        // send every call to ".../api/*" instead of the versioned API.
        using var client = new SendlyClient("test_api_key");

        var baseAddress = BaseAddressOf(client);

        Assert.Equal("https://sendly.live/api/v1/", baseAddress.ToString());
        Assert.Equal("https://sendly.live/api/v1/messages",
            new Uri(baseAddress, "messages").ToString());
    }

    [Fact]
    public void Constructor_WithCustomBaseUrlLackingTrailingSlash_KeepsLastSegment()
    {
        var options = new SendlyClientOptions { BaseUrl = "https://custom.api.com/api/v1" };

        using var client = new SendlyClient("test_api_key", options);

        Assert.Equal("https://custom.api.com/api/v1/messages",
            new Uri(BaseAddressOf(client), "messages").ToString());
    }

    [Fact]
    public void Constructor_WithCustomBaseUrlEndingInSlash_DoesNotDoubleIt()
    {
        var options = new SendlyClientOptions { BaseUrl = "https://custom.api.com/api/v1/" };

        using var client = new SendlyClient("test_api_key", options);

        Assert.Equal("https://custom.api.com/api/v1/", BaseAddressOf(client).ToString());
    }

    [Fact]
    public async Task Request_OnDefaultBaseUrl_TargetsVersionedApiOnTheWire()
    {
        using var mockHandler = new MockHttpMessageHandler();
        mockHandler.QueueSuccessResponse(@"{""data"": [], ""has_more"": false, ""total"": 0}");

        using var client = new SendlyClient("test_api_key");
        // Swap in the recording handler but keep the base address the SDK
        // itself derived, so the assertion covers the real default.
        var field = typeof(SendlyClient).GetField("_httpClient",
            BindingFlags.NonPublic | BindingFlags.Instance);
        using var instrumented = new HttpClient(mockHandler)
        {
            BaseAddress = BaseAddressOf(client),
        };
        field!.SetValue(client, instrumented);

        await client.Messages.ListAsync();

        Assert.Equal("https://sendly.live/api/v1/messages",
            mockHandler.LastRequest?.RequestUri?.GetLeftPart(UriPartial.Path));
    }

    [Fact]
    public void Version_IsSet()
    {
        // Assert
        Assert.NotNull(SendlyClient.Version);
        Assert.NotEmpty(SendlyClient.Version);
        Assert.Matches(@"^\d+\.\d+\.\d+$", SendlyClient.Version);
    }

    [Fact]
    public void SendlyClientOptions_DefaultTimeout_Is30Seconds()
    {
        // Arrange & Act
        var options = new SendlyClientOptions();

        // Assert
        Assert.Equal(TimeSpan.FromSeconds(30), options.Timeout);
    }

    [Fact]
    public void SendlyClientOptions_DefaultMaxRetries_Is3()
    {
        // Arrange & Act
        var options = new SendlyClientOptions();

        // Assert
        Assert.Equal(3, options.MaxRetries);
    }

    [Fact]
    public void SendlyClientOptions_DefaultBaseUrl_IsNull()
    {
        // Arrange & Act
        var options = new SendlyClientOptions();

        // Assert
        Assert.Null(options.BaseUrl);
    }

    [Fact]
    public void SendlyClientOptions_CanSetCustomValues()
    {
        // Arrange & Act
        var options = new SendlyClientOptions
        {
            BaseUrl = "https://custom.api.com",
            Timeout = TimeSpan.FromMinutes(2),
            MaxRetries = 10
        };

        // Assert
        Assert.Equal("https://custom.api.com", options.BaseUrl);
        Assert.Equal(TimeSpan.FromMinutes(2), options.Timeout);
        Assert.Equal(10, options.MaxRetries);
    }

    [Fact]
    public void Dispose_CanBeCalledMultipleTimes()
    {
        // Arrange
        var client = new SendlyClient("test_api_key");

        // Act & Assert - Should not throw
        client.Dispose();
        client.Dispose();
    }

    [Fact]
    public void Client_ImplementsIDisposable()
    {
        // Assert
        Assert.True(typeof(IDisposable).IsAssignableFrom(typeof(SendlyClient)));
    }

    [Fact]
    public void Client_CanBeUsedInUsingStatement()
    {
        // Act & Assert - Should not throw
        using (var client = new SendlyClient("test_api_key"))
        {
            Assert.NotNull(client);
        }
    }

    [Fact]
    public void MessagesResource_IsInitializedOnConstruction()
    {
        // Arrange & Act
        using var client = new SendlyClient("test_api_key");

        // Assert
        Assert.NotNull(client.Messages);
    }

    [Fact]
    public void Constructor_SetsAuthorizationHeader()
    {
        // This test verifies the client is properly configured
        // The actual header verification happens in integration tests
        using var client = new SendlyClient("test_api_key_123");
        Assert.NotNull(client);
    }

    [Theory]
    [InlineData("sk_test_123")]
    [InlineData("sk_live_456")]
    [InlineData("custom_key_789")]
    public void Constructor_AcceptsVariousApiKeyFormats(string apiKey)
    {
        // Act
        using var client = new SendlyClient(apiKey);

        // Assert
        Assert.NotNull(client);
    }

    [Fact]
    public void Constructor_WithZeroMaxRetries_AcceptsValue()
    {
        // Arrange
        var options = new SendlyClientOptions { MaxRetries = 0 };

        // Act
        using var client = new SendlyClient("test_api_key", options);

        // Assert
        Assert.NotNull(client);
    }

    [Fact]
    public void Constructor_WithNegativeMaxRetries_AcceptsValue()
    {
        // This tests that the client doesn't validate max retries in constructor
        // (validation happens at runtime if needed)
        var options = new SendlyClientOptions { MaxRetries = -1 };

        using var client = new SendlyClient("test_api_key", options);

        Assert.NotNull(client);
    }
}
