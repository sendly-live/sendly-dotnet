using System.Net;
using System.Reflection;
using Sendly.Exceptions;
using Sendly.Models;
using Sendly.Tests.Fixtures;
using Xunit;

namespace Sendly.Tests;

/// <summary>
/// Tests for MessagesResource RCS sends - native text and card sends, and the
/// SMS fallback shape.
/// </summary>
public class MessagesRcsTests : IDisposable
{
    private readonly MockHttpMessageHandler _mockHandler;
    private readonly HttpClient _httpClient;
    private readonly SendlyClient _client;

    public MessagesRcsTests()
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
    }

    [Fact]
    public async Task SendAsync_RcsText_ReturnsNativeRcsMessage()
    {
        // Arrange
        var responseJson = @"{
            ""id"": ""msg_123"",
            ""channel"": ""rcs"",
            ""message_format"": ""rcs"",
            ""to"": ""+15551234567"",
            ""from"": ""Acme Inc"",
            ""text"": ""Your order has shipped!"",
            ""status"": ""queued"",
            ""segments"": 1,
            ""creditsUsed"": 2,
            ""rcs"": {
                ""kind"": ""text"",
                ""agentId"": ""agent_123"",
                ""agentName"": ""Acme Inc""
            },
            ""createdAt"": ""2026-07-01T10:00:00Z"",
            ""metadata"": {}
        }";
        _mockHandler.QueueSuccessResponse(responseJson);

        // Act
        var message = await _client.Messages.SendAsync(new SendRcsMessageRequest(
            "+15551234567",
            text: "Your order has shipped!"
        ));

        // Assert
        Assert.NotNull(message);
        Assert.Equal("msg_123", message.Id);
        Assert.Equal("rcs", message.Channel);
        Assert.Equal("rcs", message.MessageFormat);
        Assert.Equal("+15551234567", message.To);
        Assert.Equal("Acme Inc", message.From);
        Assert.Equal("Your order has shipped!", message.Text);
        Assert.Equal(1, message.Segments);
        Assert.Equal(2, message.CreditsUsed);
        Assert.Equal("text", message.Rcs.Kind);
        Assert.Equal("agent_123", message.Rcs.AgentId);
        Assert.Equal("Acme Inc", message.Rcs.AgentName);
        // native send: no fallback
        Assert.Null(message.FellBackTo);
        Assert.False(message.FellBackToSms);
        Assert.Null(message.Rcs.RequestedChannel);
        Assert.Null(message.Rcs.SuggestionsDropped);
    }

    [Fact]
    public async Task SendAsync_RcsText_SendsCorrectBody()
    {
        // Arrange
        _mockHandler.QueueSuccessResponse(@"{""id"": ""msg_123"", ""rcs"": {""kind"": ""text""}}");

        // Act
        await _client.Messages.SendAsync(new SendRcsMessageRequest(
            "+15551234567",
            text: "Your order has shipped!"
        ));

        // Assert
        var request = _mockHandler.LastRequest;
        Assert.NotNull(request);
        Assert.Equal(HttpMethod.Post, request!.Method);
        Assert.Contains("messages", request.RequestUri?.ToString());
        var body = await request.Content!.ReadAsStringAsync();
        Assert.Contains("\"channel\":\"rcs\"", body);
        Assert.Contains("\"to\":\"\\u002B15551234567\"", body);
        Assert.Contains("\"text\":\"Your order has shipped!\"", body);
        // null optionals should be omitted
        Assert.DoesNotContain("agentId", body);
        Assert.DoesNotContain("card", body);
        Assert.DoesNotContain("suggestions", body);
        Assert.DoesNotContain("fallbackToSms", body);
    }

    [Fact]
    public async Task SendAsync_RcsTextWithSuggestions_SendsChips()
    {
        // Arrange
        _mockHandler.QueueSuccessResponse(@"{""id"": ""msg_123"", ""rcs"": {""kind"": ""text""}}");

        // Act
        await _client.Messages.SendAsync(new SendRcsMessageRequest(
            "+15551234567",
            text: "Your order has shipped!",
            agentId: "agent_123",
            suggestions: new()
            {
                RcsSuggestion.CreateReply("Thanks", "thanks"),
                RcsSuggestion.CreateAction("Track", "track", "https://example.com/track/4821")
            }
        ));

        // Assert
        var body = await _mockHandler.LastRequest!.Content!.ReadAsStringAsync();
        Assert.Contains("\"agentId\":\"agent_123\"", body);
        Assert.Contains("\"reply\":{\"text\":\"Thanks\",\"postbackData\":\"thanks\"}", body);
        Assert.Contains("\"action\":{\"text\":\"Track\",\"postbackData\":\"track\",\"url\":\"https://example.com/track/4821\"}", body);
        // each chip carries exactly one of reply/action
        Assert.DoesNotContain("\"reply\":null", body);
        Assert.DoesNotContain("\"action\":null", body);
    }

    [Fact]
    public async Task SendAsync_RcsCard_ReturnsCardMessage()
    {
        // Arrange
        var responseJson = @"{
            ""id"": ""msg_123"",
            ""channel"": ""rcs"",
            ""message_format"": ""rcs"",
            ""to"": ""+15551234567"",
            ""from"": ""Acme Inc"",
            ""text"": null,
            ""status"": ""queued"",
            ""segments"": 1,
            ""creditsUsed"": 2,
            ""rcs"": {
                ""kind"": ""card"",
                ""agentId"": ""agent_123"",
                ""agentName"": ""Acme Inc""
            },
            ""createdAt"": ""2026-07-01T10:00:00Z"",
            ""metadata"": {}
        }";
        _mockHandler.QueueSuccessResponse(responseJson);

        // Act
        var message = await _client.Messages.SendAsync(new SendRcsMessageRequest(
            "+15551234567",
            card: new RcsCard
            {
                Title = "Order #4821 shipped",
                Description = "Arriving Thursday.",
                MediaUrl = "https://example.com/box.jpg",
                Orientation = "vertical",
                Suggestions = new() { RcsSuggestion.CreateAction("Track", "track", "https://example.com/track/4821") }
            }
        ));

        // Assert
        Assert.Null(message.Text);
        Assert.Equal("card", message.Rcs.Kind);
        Assert.Equal(2, message.CreditsUsed);
        Assert.False(message.FellBackToSms);

        var body = await _mockHandler.LastRequest!.Content!.ReadAsStringAsync();
        Assert.Contains("\"card\":{\"title\":\"Order #4821 shipped\",\"description\":\"Arriving Thursday.\"", body);
        Assert.Contains("\"mediaUrl\":\"https://example.com/box.jpg\"", body);
        Assert.Contains("\"orientation\":\"vertical\"", body);
        // no top-level text alongside a card — "card" follows "to" directly
        Assert.Contains("\"to\":\"\\u002B15551234567\",\"card\":", body);
    }

    [Fact]
    public async Task SendAsync_RcsFallsBackToSms_ExposesFallbackShape()
    {
        // Arrange — recipient isn't RCS-capable, so the text leg went out as SMS
        var responseJson = @"{
            ""id"": ""msg_123"",
            ""channel"": ""sms"",
            ""fellBackTo"": ""sms"",
            ""message_format"": ""sms"",
            ""to"": ""+15551234567"",
            ""from"": ""+15559876543"",
            ""text"": ""Your order has shipped!"",
            ""status"": ""queued"",
            ""segments"": 1,
            ""creditsUsed"": 2,
            ""rcs"": {
                ""requestedChannel"": ""rcs"",
                ""agentId"": ""agent_123"",
                ""suggestionsDropped"": true
            },
            ""createdAt"": ""2026-07-01T10:00:00Z"",
            ""metadata"": {}
        }";
        _mockHandler.QueueSuccessResponse(responseJson);

        // Act
        var message = await _client.Messages.SendAsync(new SendRcsMessageRequest(
            "+15551234567",
            text: "Your order has shipped!",
            suggestions: new() { RcsSuggestion.CreateReply("Thanks", "thanks") }
        ));

        // Assert
        Assert.Equal("sms", message.Channel);
        Assert.Equal("sms", message.MessageFormat);
        Assert.Equal("sms", message.FellBackTo);
        Assert.True(message.FellBackToSms);
        Assert.Equal("+15559876543", message.From);
        Assert.Equal("rcs", message.Rcs.RequestedChannel);
        Assert.Equal("agent_123", message.Rcs.AgentId);
        Assert.True(message.Rcs.SuggestionsDropped);
        // fallback carries no native-send details
        Assert.Null(message.Rcs.Kind);
        Assert.Null(message.Rcs.AgentName);
    }

    [Fact]
    public async Task SendAsync_RcsWithFallbackDisabled_SendsFlag()
    {
        // Arrange
        _mockHandler.QueueSuccessResponse(@"{""id"": ""msg_123"", ""rcs"": {""kind"": ""text""}}");

        // Act
        await _client.Messages.SendAsync(new SendRcsMessageRequest(
            "+15551234567",
            text: "RCS only",
            fallbackToSms: false
        ));

        // Assert
        var body = await _mockHandler.LastRequest!.Content!.ReadAsStringAsync();
        Assert.Contains("\"fallbackToSms\":false", body);
    }

    [Fact]
    public async Task SendAsync_RcsMessage_UnwrapsMessageEnvelope()
    {
        // Arrange
        var responseJson = @"{
            ""message"": {
                ""id"": ""msg_456"",
                ""channel"": ""rcs"",
                ""rcs"": {""kind"": ""text"", ""agentId"": ""agent_123""}
            }
        }";
        _mockHandler.QueueSuccessResponse(responseJson);

        // Act
        var message = await _client.Messages.SendAsync(new SendRcsMessageRequest(
            "+15551234567",
            text: "Hello"
        ));

        // Assert
        Assert.Equal("msg_456", message.Id);
        Assert.Equal("text", message.Rcs.Kind);
        Assert.Equal("agent_123", message.Rcs.AgentId);
    }

    [Fact]
    public async Task SendAsync_RcsWithoutBody_ThrowsValidationException()
    {
        // Act & Assert
        var exception = await Assert.ThrowsAsync<ValidationException>(
            () => _client.Messages.SendAsync(new SendRcsMessageRequest("+15551234567")));

        Assert.Contains("Provide exactly one of 'text' or 'card'", exception.Message);
    }

    [Fact]
    public async Task SendAsync_RcsWithBothTextAndCard_ThrowsValidationException()
    {
        // Act & Assert
        var exception = await Assert.ThrowsAsync<ValidationException>(
            () => _client.Messages.SendAsync(new SendRcsMessageRequest(
                "+15551234567",
                text: "Hello",
                card: new RcsCard { Title = "Hi", Description = "There" }
            )));

        Assert.Contains("Provide exactly one of 'text' or 'card'", exception.Message);
    }

    [Fact]
    public async Task SendAsync_RcsCardWithTopLevelSuggestions_ThrowsValidationException()
    {
        // Act & Assert
        var exception = await Assert.ThrowsAsync<ValidationException>(
            () => _client.Messages.SendAsync(new SendRcsMessageRequest(
                "+15551234567",
                card: new RcsCard { Title = "Hi", Description = "There" },
                suggestions: new() { RcsSuggestion.CreateReply("Thanks", "thanks") }
            )));

        Assert.Contains("card.suggestions", exception.Message);
    }

    [Theory]
    [InlineData("")]
    [InlineData("15551234567")]
    [InlineData("invalid")]
    public async Task SendAsync_RcsWithInvalidTo_ThrowsValidationException(string invalidTo)
    {
        // Act & Assert
        await Assert.ThrowsAsync<ValidationException>(
            () => _client.Messages.SendAsync(new SendRcsMessageRequest(invalidTo, text: "Hello")));
    }

    [Fact]
    public async Task SendAsync_RcsWith402Response_ThrowsInsufficientCreditsException()
    {
        // Arrange
        _mockHandler.QueueResponse(HttpStatusCode.PaymentRequired, @"{""message"": ""Insufficient credits""}");

        // Act & Assert
        await Assert.ThrowsAsync<InsufficientCreditsException>(
            () => _client.Messages.SendAsync(new SendRcsMessageRequest("+15551234567", text: "Hello")));
    }

    [Fact]
    public async Task SendAsync_RcsWhenChannelDark_ThrowsNotFoundException()
    {
        // Arrange — rcs_channel flag off for the account
        _mockHandler.QueueResponse(HttpStatusCode.NotFound,
            @"{""error"": ""rcs_not_enabled"", ""message"": ""RCS is not enabled for your account.""}");

        // Act & Assert
        await Assert.ThrowsAsync<NotFoundException>(
            () => _client.Messages.SendAsync(new SendRcsMessageRequest("+15551234567", text: "Hello")));
    }
}
