using System.Net;
using System.Reflection;
using Sendly.Exceptions;
using Sendly.Models;
using Sendly.Tests.Fixtures;
using Xunit;

namespace Sendly.Tests;

/// <summary>
/// Tests for MessagesResource WhatsApp sends - text, media with caption, and
/// template variants.
/// </summary>
public class MessagesWhatsAppTests : IDisposable
{
    private readonly MockHttpMessageHandler _mockHandler;
    private readonly HttpClient _httpClient;
    private readonly SendlyClient _client;

    public MessagesWhatsAppTests()
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
    public async Task SendAsync_WhatsAppText_ReturnsWhatsAppMessage()
    {
        // Arrange
        var responseJson = @"{
            ""id"": ""msg_123"",
            ""channel"": ""whatsapp"",
            ""message_format"": ""whatsapp"",
            ""to"": ""+15551234567"",
            ""from"": ""+15559876543"",
            ""text"": ""Your table is ready!"",
            ""status"": ""queued"",
            ""segments"": 1,
            ""creditsUsed"": 1,
            ""whatsapp"": {
                ""kind"": ""text"",
                ""messageId"": null
            },
            ""createdAt"": ""2026-07-01T10:00:00Z"",
            ""metadata"": {}
        }";
        _mockHandler.QueueSuccessResponse(responseJson);

        // Act
        var message = await _client.Messages.SendAsync(new SendWhatsAppMessageRequest(
            "+15551234567",
            "+15559876543",
            text: "Your table is ready!"
        ));

        // Assert
        Assert.NotNull(message);
        Assert.Equal("msg_123", message.Id);
        Assert.Equal("whatsapp", message.Channel);
        Assert.Equal("whatsapp", message.MessageFormat);
        Assert.Equal("+15551234567", message.To);
        Assert.Equal("+15559876543", message.From);
        Assert.Equal("Your table is ready!", message.Text);
        Assert.Equal(1, message.Segments);
        Assert.Equal(1, message.CreditsUsed);
        Assert.Equal("text", message.WhatsApp.Kind);
        Assert.Null(message.WhatsApp.Template);
        Assert.Null(message.WhatsApp.MessageId);
    }

    [Fact]
    public async Task SendAsync_WhatsAppText_SendsCorrectBody()
    {
        // Arrange
        _mockHandler.QueueSuccessResponse(@"{""id"": ""msg_123"", ""whatsapp"": {""kind"": ""text""}}");

        // Act
        await _client.Messages.SendAsync(new SendWhatsAppMessageRequest(
            "+15551234567",
            "+15559876543",
            text: "Your table is ready!"
        ));

        // Assert
        var request = _mockHandler.LastRequest;
        Assert.NotNull(request);
        Assert.Equal(HttpMethod.Post, request!.Method);
        Assert.Contains("messages", request.RequestUri?.ToString());
        var body = await request.Content!.ReadAsStringAsync();
        Assert.Contains("\"channel\":\"whatsapp\"", body);
        Assert.Contains("\"to\":\"\\u002B15551234567\"", body);
        Assert.Contains("\"from\":\"\\u002B15559876543\"", body);
        Assert.Contains("\"text\":\"Your table is ready!\"", body);
        // null optionals should be omitted
        Assert.DoesNotContain("mediaUrls", body);
        Assert.DoesNotContain("template", body);
    }

    [Fact]
    public async Task SendAsync_WhatsAppMedia_SendsCaptionAndMediaUrls()
    {
        // Arrange
        var responseJson = @"{
            ""id"": ""msg_123"",
            ""channel"": ""whatsapp"",
            ""message_format"": ""whatsapp"",
            ""to"": ""+15551234567"",
            ""from"": ""+15559876543"",
            ""text"": null,
            ""status"": ""queued"",
            ""segments"": 1,
            ""creditsUsed"": 1,
            ""whatsapp"": {
                ""kind"": ""media"",
                ""messageId"": null
            },
            ""createdAt"": ""2026-07-01T10:00:00Z""
        }";
        _mockHandler.QueueSuccessResponse(responseJson);

        // Act
        var message = await _client.Messages.SendAsync(new SendWhatsAppMessageRequest(
            "+15551234567",
            "+15559876543",
            text: "Here's your receipt",
            mediaUrls: new() { "https://example.com/receipt.pdf" }
        ));

        // Assert
        Assert.Equal("media", message.WhatsApp.Kind);
        var body = await _mockHandler.LastRequest!.Content!.ReadAsStringAsync();
        Assert.Contains("\"mediaUrls\":[\"https://example.com/receipt.pdf\"]", body);
        Assert.Contains("\"text\":\"Here", body);
    }

    [Fact]
    public async Task SendAsync_WhatsAppTemplate_ReturnsTemplateDetails()
    {
        // Arrange
        var responseJson = @"{
            ""id"": ""msg_123"",
            ""channel"": ""whatsapp"",
            ""message_format"": ""whatsapp"",
            ""to"": ""+15551234567"",
            ""from"": ""+15559876543"",
            ""text"": null,
            ""status"": ""queued"",
            ""segments"": 1,
            ""creditsUsed"": 2,
            ""whatsapp"": {
                ""kind"": ""template"",
                ""template"": {
                    ""name"": ""order_shipped"",
                    ""language"": ""en_US"",
                    ""category"": ""utility""
                },
                ""messageId"": null
            },
            ""createdAt"": ""2026-07-01T10:00:00Z""
        }";
        _mockHandler.QueueSuccessResponse(responseJson);

        // Act
        var message = await _client.Messages.SendAsync(new SendWhatsAppMessageRequest(
            "+15551234567",
            "+15559876543",
            template: new WhatsAppTemplateSendParams
            {
                Name = "order_shipped",
                Language = "en_US",
                Variables = new() { ["1"] = "Sam", ["2"] = "#4821" }
            }
        ));

        // Assert
        Assert.Null(message.Text);
        Assert.Equal(2, message.CreditsUsed);
        Assert.Equal("template", message.WhatsApp.Kind);
        Assert.NotNull(message.WhatsApp.Template);
        Assert.Equal("order_shipped", message.WhatsApp.Template!.Name);
        Assert.Equal("en_US", message.WhatsApp.Template.Language);
        Assert.Equal("utility", message.WhatsApp.Template.Category);
    }

    [Fact]
    public async Task SendAsync_WhatsAppTemplate_SendsCorrectBody()
    {
        // Arrange
        _mockHandler.QueueSuccessResponse(@"{""id"": ""msg_123"", ""whatsapp"": {""kind"": ""template""}}");

        // Act
        await _client.Messages.SendAsync(new SendWhatsAppMessageRequest(
            "+15551234567",
            "+15559876543",
            template: new WhatsAppTemplateSendParams
            {
                Name = "order_shipped",
                Language = "en_US",
                Variables = new() { ["1"] = "Sam", ["2"] = "#4821" },
                Buttons = new()
                {
                    new WhatsAppTemplateButtonVariables
                    {
                        Index = 0,
                        Variables = new() { ["1"] = "4821" }
                    }
                }
            }
        ));

        // Assert
        var body = await _mockHandler.LastRequest!.Content!.ReadAsStringAsync();
        Assert.Contains("\"channel\":\"whatsapp\"", body);
        Assert.Contains("\"template\":{\"name\":\"order_shipped\",\"language\":\"en_US\"", body);
        Assert.Contains("\"variables\":{\"1\":\"Sam\",\"2\":\"#4821\"}", body);
        Assert.Contains("\"buttons\":[{\"index\":0,\"variables\":{\"1\":\"4821\"}}]", body);
        Assert.DoesNotContain("\"text\"", body);
    }

    [Fact]
    public async Task SendAsync_WhatsAppMessage_UnwrapsMessageEnvelope()
    {
        // Arrange
        var responseJson = @"{
            ""message"": {
                ""id"": ""msg_456"",
                ""channel"": ""whatsapp"",
                ""whatsapp"": {""kind"": ""text"", ""messageId"": null}
            }
        }";
        _mockHandler.QueueSuccessResponse(responseJson);

        // Act
        var message = await _client.Messages.SendAsync(new SendWhatsAppMessageRequest(
            "+15551234567",
            "+15559876543",
            text: "Hello"
        ));

        // Assert
        Assert.Equal("msg_456", message.Id);
        Assert.Equal("text", message.WhatsApp.Kind);
    }

    [Fact]
    public async Task SendAsync_WhatsAppWithoutBody_ThrowsValidationException()
    {
        // Act & Assert
        var exception = await Assert.ThrowsAsync<ValidationException>(
            () => _client.Messages.SendAsync(new SendWhatsAppMessageRequest(
                "+15551234567",
                "+15559876543"
            )));

        Assert.Contains("Provide 'text', 'mediaUrls', or 'template'", exception.Message);
    }

    [Theory]
    [InlineData("")]
    [InlineData("15559876543")]
    [InlineData("invalid")]
    public async Task SendAsync_WhatsAppWithInvalidFrom_ThrowsValidationException(string invalidFrom)
    {
        // Act & Assert
        await Assert.ThrowsAsync<ValidationException>(
            () => _client.Messages.SendAsync(new SendWhatsAppMessageRequest(
                "+15551234567",
                invalidFrom,
                text: "Hello"
            )));
    }

    [Fact]
    public async Task SendAsync_WhatsAppWithInvalidTo_ThrowsValidationException()
    {
        // Act & Assert
        await Assert.ThrowsAsync<ValidationException>(
            () => _client.Messages.SendAsync(new SendWhatsAppMessageRequest(
                "invalid",
                "+15559876543",
                text: "Hello"
            )));
    }

    [Fact]
    public async Task SendAsync_WhatsAppWith402Response_ThrowsInsufficientCreditsException()
    {
        // Arrange
        _mockHandler.QueueResponse(HttpStatusCode.PaymentRequired, @"{""message"": ""Insufficient credits""}");

        // Act & Assert
        await Assert.ThrowsAsync<InsufficientCreditsException>(
            () => _client.Messages.SendAsync(new SendWhatsAppMessageRequest(
                "+15551234567",
                "+15559876543",
                text: "Hello"
            )));
    }
}
