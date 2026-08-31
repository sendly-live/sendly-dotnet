using System.Net;
using System.Reflection;
using Sendly.Exceptions;
using Sendly.Models;
using Sendly.Tests.Fixtures;
using Xunit;

namespace Sendly.Tests;

/// <summary>
/// Tests for batch message operations.
/// </summary>
public class MessagesBatchTests : IDisposable
{
    private readonly MockHttpMessageHandler _mockHandler;
    private readonly HttpClient _httpClient;
    private readonly SendlyClient _client;

    public MessagesBatchTests()
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

    #region SendBatchAsync Tests

    [Fact]
    public async Task SendBatchAsync_WithValidMessages_ReturnsBatchResponse()
    {
        // Arrange
        // Exactly the payload POST /messages/batch puts on the wire: camelCase
        // keys, no "queued", no "createdAt".
        var responseJson = @"{
            ""batchId"": ""batch_123"",
            ""status"": ""completed"",
            ""total"": 2,
            ""sent"": 2,
            ""failed"": 0,
            ""optedOutSkipped"": 0,
            ""invalidSkipped"": 0,
            ""creditsUsed"": 2,
            ""creditsRefunded"": 0,
            ""messages"": [
                {
                    ""index"": 0,
                    ""id"": ""msg_1"",
                    ""to"": ""+15551234567"",
                    ""status"": ""queued""
                },
                {
                    ""index"": 1,
                    ""id"": ""msg_2"",
                    ""to"": ""+15559876543"",
                    ""status"": ""queued""
                }
            ]
        }";
        _mockHandler.QueueSuccessResponse(responseJson);

        var request = new SendBatchRequest
        {
            Messages = new List<BatchMessageItem>
            {
                new BatchMessageItem("+15551234567", "Message 1"),
                new BatchMessageItem("+15559876543", "Message 2")
            }
        };

        // Act
        var response = await _client.Messages.SendBatchAsync(request);

        // Assert
        Assert.NotNull(response);
        Assert.Equal("batch_123", response.BatchId);
        Assert.Equal(2, response.Total);
        Assert.Equal(2, response.Sent);
        Assert.Equal(0, response.Failed);
        Assert.Equal(0, response.OptedOutSkipped);
        Assert.Equal(0, response.InvalidSkipped);
        Assert.Equal(2, response.CreditsUsed);
        Assert.Equal(0, response.CreditsRefunded);
        Assert.Equal("completed", response.Status);
        Assert.Equal(2, response.Messages.Count);
        Assert.Equal("msg_1", response.Messages[0].Id);
        // Absent from a send response, not defaulted to zero.
        Assert.Null(response.QueuedCount);
        Assert.Null(response.CreatedAt);
    }

    [Fact]
    public async Task SendBatchAsync_WithPartialFailures_ReturnsCorrectCounts()
    {
        // Arrange
        var responseJson = @"{
            ""batchId"": ""batch_456"",
            ""status"": ""partial_failure"",
            ""total"": 3,
            ""sent"": 2,
            ""failed"": 1,
            ""optedOutSkipped"": 0,
            ""invalidSkipped"": 0,
            ""creditsUsed"": 2,
            ""creditsRefunded"": 1,
            ""messages"": [
                {
                    ""index"": 0,
                    ""id"": ""msg_1"",
                    ""to"": ""+15551234567"",
                    ""status"": ""queued""
                },
                {
                    ""index"": 1,
                    ""to"": ""+15559999999"",
                    ""status"": ""failed"",
                    ""error"": ""Invalid phone number""
                },
                {
                    ""index"": 2,
                    ""id"": ""msg_3"",
                    ""to"": ""+15558888888"",
                    ""status"": ""queued""
                }
            ]
        }";
        _mockHandler.QueueSuccessResponse(responseJson);

        var request = new SendBatchRequest
        {
            Messages = new List<BatchMessageItem>
            {
                new BatchMessageItem("+15551234567", "Message 1"),
                new BatchMessageItem("+15559999999", "Message 2"),
                new BatchMessageItem("+15558888888", "Message 3")
            }
        };

        // Act
        var response = await _client.Messages.SendBatchAsync(request);

        // Assert
        Assert.Equal(3, response.Total);
        Assert.Equal(2, response.Sent);
        Assert.Equal(1, response.Failed);
        Assert.Equal(2, response.CreditsUsed);
        Assert.Equal(1, response.CreditsRefunded);
        Assert.True(response.IsPartialFailure);

        // Verify failed result has error details
        var failedResult = response.Messages[1];
        Assert.True(failedResult.IsFailed);
        Assert.Equal("Invalid phone number", failedResult.Error);
    }

    [Fact]
    public async Task SendBatchAsync_WithEmptyMessageList_ThrowsValidationException()
    {
        // Arrange
        var request = new SendBatchRequest
        {
            Messages = new List<BatchMessageItem>()
        };

        // Act & Assert
        var exception = await Assert.ThrowsAsync<ValidationException>(
            () => _client.Messages.SendBatchAsync(request));

        Assert.Contains("At least one message is required", exception.Message);
    }

    [Fact]
    public async Task SendBatchAsync_WithNullMessageList_ThrowsValidationException()
    {
        // Arrange
        var request = new SendBatchRequest
        {
            Messages = null!
        };

        // Act & Assert
        await Assert.ThrowsAsync<ValidationException>(
            () => _client.Messages.SendBatchAsync(request));
    }

    [Fact]
    public async Task SendBatchAsync_WithInvalidPhoneInBatch_ThrowsValidationException()
    {
        // Arrange
        var request = new SendBatchRequest
        {
            Messages = new List<BatchMessageItem>
            {
                new BatchMessageItem("+15551234567", "Valid"),
                new BatchMessageItem("invalid", "Invalid phone")
            }
        };

        // Act & Assert
        await Assert.ThrowsAsync<ValidationException>(
            () => _client.Messages.SendBatchAsync(request));
    }

    [Fact]
    public async Task SendBatchAsync_WithEmptyTextInBatch_ThrowsValidationException()
    {
        // Arrange
        var request = new SendBatchRequest
        {
            Messages = new List<BatchMessageItem>
            {
                new BatchMessageItem("+15551234567", "Valid"),
                new BatchMessageItem("+15559876543", "")
            }
        };

        // Act & Assert
        var exception = await Assert.ThrowsAsync<ValidationException>(
            () => _client.Messages.SendBatchAsync(request));

        Assert.Contains("Message text is required", exception.Message);
    }

    [Fact]
    public async Task SendBatchAsync_WithTooLongTextInBatch_ThrowsValidationException()
    {
        // Arrange
        var longText = new string('a', 1601);
        var request = new SendBatchRequest
        {
            Messages = new List<BatchMessageItem>
            {
                new BatchMessageItem("+15551234567", longText)
            }
        };

        // Act & Assert
        await Assert.ThrowsAsync<ValidationException>(
            () => _client.Messages.SendBatchAsync(request));
    }

    [Fact]
    public async Task SendBatchAsync_WithSingleMessage_Succeeds()
    {
        // Arrange
        var responseJson = @"{
            ""batchId"": ""batch_single"",
            ""status"": ""completed"",
            ""total"": 1,
            ""sent"": 1,
            ""failed"": 0,
            ""optedOutSkipped"": 0,
            ""invalidSkipped"": 0,
            ""creditsUsed"": 1,
            ""creditsRefunded"": 0,
            ""messages"": [
                {
                    ""index"": 0,
                    ""id"": ""msg_1"",
                    ""to"": ""+15551234567"",
                    ""status"": ""queued""
                }
            ]
        }";
        _mockHandler.QueueSuccessResponse(responseJson);

        var request = new SendBatchRequest
        {
            Messages = new List<BatchMessageItem>
            {
                new BatchMessageItem("+15551234567", "Single message")
            }
        };

        // Act
        var response = await _client.Messages.SendBatchAsync(request);

        // Assert
        Assert.Equal(1, response.Total);
        Assert.Single(response.Messages);
    }

    [Fact]
    public async Task SendBatchAsync_WithLargeBatch_Succeeds()
    {
        // Arrange
        var messages = new List<BatchMessageItem>();
        var results = new System.Text.StringBuilder("[");

        for (int i = 0; i < 100; i++)
        {
            messages.Add(new BatchMessageItem($"+1555123{i:D4}", $"Message {i}"));

            if (i > 0) results.Append(",");
            results.Append($@"{{
                ""index"": {i},
                ""id"": ""msg_{i}"",
                ""to"": ""+1555123{i:D4}"",
                ""status"": ""queued""
            }}");
        }
        results.Append("]");

        var responseJson = $@"{{
            ""batchId"": ""batch_large"",
            ""status"": ""completed"",
            ""total"": 100,
            ""sent"": 100,
            ""failed"": 0,
            ""optedOutSkipped"": 0,
            ""invalidSkipped"": 0,
            ""creditsUsed"": 100,
            ""creditsRefunded"": 0,
            ""messages"": {results}
        }}";
        _mockHandler.QueueSuccessResponse(responseJson);

        var request = new SendBatchRequest { Messages = messages };

        // Act
        var response = await _client.Messages.SendBatchAsync(request);

        // Assert
        Assert.Equal(100, response.Total);
        Assert.Equal(100, response.Messages.Count);
    }

    [Fact]
    public async Task SendBatchAsync_With401Response_ThrowsAuthenticationException()
    {
        // Arrange
        _mockHandler.QueueResponse(HttpStatusCode.Unauthorized,
            @"{""message"": ""Invalid API key""}");

        var request = new SendBatchRequest
        {
            Messages = new List<BatchMessageItem>
            {
                new BatchMessageItem("+15551234567", "Test")
            }
        };

        // Act & Assert
        await Assert.ThrowsAsync<AuthenticationException>(
            () => _client.Messages.SendBatchAsync(request));
    }

    [Fact]
    public async Task SendBatchAsync_With402Response_ThrowsInsufficientCreditsException()
    {
        // Arrange
        _mockHandler.QueueResponse(HttpStatusCode.PaymentRequired,
            @"{""error"": ""Insufficient credits for batch""}");

        var request = new SendBatchRequest
        {
            Messages = new List<BatchMessageItem>
            {
                new BatchMessageItem("+15551234567", "Test")
            }
        };

        // Act & Assert
        await Assert.ThrowsAsync<InsufficientCreditsException>(
            () => _client.Messages.SendBatchAsync(request));
    }

    [Fact]
    public async Task SendBatchAsync_With429Response_ThrowsRateLimitException()
    {
        // Arrange - Queue multiple 429 responses for all retry attempts
        for (int i = 0; i < 4; i++)
        {
            var response = new HttpResponseMessage(HttpStatusCode.TooManyRequests)
            {
                Content = new StringContent(@"{""message"": ""Rate limit exceeded""}",
                    System.Text.Encoding.UTF8, "application/json")
            };
            response.Headers.Add("Retry-After", "1");
            _mockHandler.QueueResponse(response);
        }

        var request = new SendBatchRequest
        {
            Messages = new List<BatchMessageItem>
            {
                new BatchMessageItem("+15551234567", "Test")
            }
        };

        // Act & Assert
        var exception = await Assert.ThrowsAsync<RateLimitException>(
            () => _client.Messages.SendBatchAsync(request));

        Assert.Equal(TimeSpan.FromSeconds(1), exception.RetryAfter);
    }

    [Fact]
    public async Task SendBatchAsync_With500Response_RetriesAndThrows()
    {
        // Arrange
        for (int i = 0; i < 4; i++)
        {
            _mockHandler.QueueResponse(HttpStatusCode.InternalServerError,
                @"{""error"": ""Server error""}");
        }

        var request = new SendBatchRequest
        {
            Messages = new List<BatchMessageItem>
            {
                new BatchMessageItem("+15551234567", "Test")
            }
        };

        // Act & Assert
        await Assert.ThrowsAsync<SendlyException>(
            () => _client.Messages.SendBatchAsync(request));

        Assert.Equal(4, _mockHandler.Requests.Count);
    }

    #endregion

    #region GetBatchAsync Tests

    [Fact]
    public async Task GetBatchAsync_WithValidId_ReturnsBatchResponse()
    {
        // Arrange
        // The batch read endpoint names the identifier "id" and does report
        // queued/createdAt.
        var responseJson = @"{
            ""id"": ""batch_get_123"",
            ""status"": ""completed"",
            ""total"": 2,
            ""queued"": 0,
            ""sent"": 2,
            ""delivered"": 2,
            ""failed"": 0,
            ""creditsReserved"": 2,
            ""creditsUsed"": 2,
            ""creditsRefunded"": 0,
            ""createdAt"": ""2024-01-20T10:00:00Z"",
            ""completedAt"": ""2024-01-20T10:05:00Z"",
            ""messages"": [
                {
                    ""id"": ""msg_1"",
                    ""to"": ""+15551234567"",
                    ""status"": ""delivered""
                },
                {
                    ""id"": ""msg_2"",
                    ""to"": ""+15559876543"",
                    ""status"": ""delivered""
                }
            ]
        }";
        _mockHandler.QueueSuccessResponse(responseJson);

        // Act
        var response = await _client.Messages.GetBatchAsync("batch_get_123");

        // Assert
        Assert.NotNull(response);
        Assert.Equal("batch_get_123", response.BatchId);
        Assert.Equal("completed", response.Status);
        Assert.Equal(2, response.Messages.Count);
        Assert.Equal(0, response.QueuedCount);
        Assert.Equal(2, response.CreditsUsed);
        Assert.NotNull(response.CreatedAt);
        Assert.NotNull(response.CompletedAt);
    }

    [Fact]
    public async Task GetBatchAsync_WithEmptyId_ThrowsValidationException()
    {
        // Act & Assert
        var exception = await Assert.ThrowsAsync<ValidationException>(
            () => _client.Messages.GetBatchAsync(""));

        Assert.Contains("Batch ID is required", exception.Message);
    }

    [Fact]
    public async Task GetBatchAsync_WithNullId_ThrowsValidationException()
    {
        // Act & Assert
        await Assert.ThrowsAsync<ValidationException>(
            () => _client.Messages.GetBatchAsync(null!));
    }

    [Fact]
    public async Task GetBatchAsync_With404Response_ThrowsNotFoundException()
    {
        // Arrange
        _mockHandler.QueueResponse(HttpStatusCode.NotFound,
            @"{""message"": ""Batch not found""}");

        // Act & Assert
        var exception = await Assert.ThrowsAsync<NotFoundException>(
            () => _client.Messages.GetBatchAsync("batch_nonexistent"));

        Assert.Equal("Batch not found", exception.Message);
        Assert.Equal(404, exception.StatusCode);
    }

    [Fact]
    public async Task GetBatchAsync_With401Response_ThrowsAuthenticationException()
    {
        // Arrange
        _mockHandler.QueueResponse(HttpStatusCode.Unauthorized,
            @"{""message"": ""Invalid API key""}");

        // Act & Assert
        await Assert.ThrowsAsync<AuthenticationException>(
            () => _client.Messages.GetBatchAsync("batch_123"));
    }

    [Fact]
    public async Task GetBatchAsync_WithSpecialCharactersInId_EncodesCorrectly()
    {
        // Arrange
        var responseJson = @"{
            ""id"": ""batch/special+id"",
            ""status"": ""completed"",
            ""total"": 1,
            ""queued"": 0,
            ""sent"": 1,
            ""failed"": 0,
            ""creditsUsed"": 1,
            ""messages"": [],
            ""createdAt"": ""2024-01-20T10:00:00Z""
        }";
        _mockHandler.QueueSuccessResponse(responseJson);

        // Act
        await _client.Messages.GetBatchAsync("batch/special+id");

        // Assert
        var request = _mockHandler.LastRequest;
        Assert.NotNull(request);
        Assert.Contains("batch%2Fspecial%2Bid", request.RequestUri?.ToString());
    }

    [Fact]
    public async Task GetBatchAsync_WithPendingStatus_ReturnsInProgressBatch()
    {
        // Arrange
        var responseJson = @"{
            ""id"": ""batch_pending"",
            ""status"": ""processing"",
            ""total"": 100,
            ""queued"": 50,
            ""sent"": 50,
            ""failed"": 0,
            ""creditsUsed"": 50,
            ""messages"": [],
            ""createdAt"": ""2024-01-20T10:00:00Z""
        }";
        _mockHandler.QueueSuccessResponse(responseJson);

        // Act
        var response = await _client.Messages.GetBatchAsync("batch_pending");

        // Assert
        Assert.Equal("processing", response.Status);
        Assert.Equal(50, response.QueuedCount);
        Assert.Equal(100, response.Total);
    }

    #endregion

    #region ListBatchesAsync Tests

    [Fact]
    public async Task ListBatchesAsync_WithoutOptions_ReturnsBatchList()
    {
        // Arrange
        var responseJson = @"{
            ""data"": [
                {
                    ""id"": ""batch_1"",
                    ""status"": ""completed"",
                    ""total"": 10,
                    ""queued"": 0,
                    ""sent"": 10,
                    ""failed"": 0,
                    ""creditsUsed"": 10,
                    ""createdAt"": ""2024-01-20T10:00:00Z""
                },
                {
                    ""id"": ""batch_2"",
                    ""status"": ""completed"",
                    ""total"": 5,
                    ""queued"": 0,
                    ""sent"": 5,
                    ""failed"": 0,
                    ""creditsUsed"": 5,
                    ""createdAt"": ""2024-01-20T11:00:00Z""
                }
            ],
            ""has_more"": false,
            ""total"": 2
        }";
        _mockHandler.QueueSuccessResponse(responseJson);

        // Act
        var result = await _client.Messages.ListBatchesAsync();

        // Assert
        Assert.NotNull(result);
        Assert.Equal(2, result.Count());
        Assert.False(result.HasMore);
        Assert.Equal(2, result.Total);
        Assert.Equal("batch_1", result.Data[0].BatchId);
        Assert.Equal(10, result.Data[0].CreditsUsed);
    }

    [Fact]
    public async Task ListBatchesAsync_WithOptions_SendsCorrectQueryParameters()
    {
        // Arrange
        var responseJson = @"{""data"": [], ""has_more"": false, ""total"": 0}";
        _mockHandler.QueueSuccessResponse(responseJson);

        var options = new ListBatchesOptions
        {
            Limit = 20,
            Offset = 40,
            Status = "completed"
        };

        // Act
        await _client.Messages.ListBatchesAsync(options);

        // Assert
        var request = _mockHandler.LastRequest;
        Assert.NotNull(request);
        Assert.Contains("limit=20", request.RequestUri?.Query);
        Assert.Contains("offset=40", request.RequestUri?.Query);
        Assert.Contains("status=completed", request.RequestUri?.Query);
    }

    [Fact]
    public async Task ListBatchesAsync_WithPagination_ReturnsCorrectData()
    {
        // Arrange
        var responseJson = @"{
            ""data"": [
                {
                    ""id"": ""batch_page1"",
                    ""status"": ""completed"",
                    ""total"": 10,
                    ""queued"": 0,
                    ""sent"": 10,
                    ""failed"": 0,
                    ""creditsUsed"": 10,
                    ""createdAt"": ""2024-01-20T10:00:00Z""
                }
            ],
            ""has_more"": true,
            ""total"": 50
        }";
        _mockHandler.QueueSuccessResponse(responseJson);

        // Act
        var result = await _client.Messages.ListBatchesAsync(new ListBatchesOptions { Limit = 1 });

        // Assert
        Assert.True(result.HasMore);
        Assert.Equal(50, result.Total);
        Assert.Single(result);
    }

    [Fact]
    public async Task ListBatchesAsync_With401Response_ThrowsAuthenticationException()
    {
        // Arrange
        _mockHandler.QueueResponse(HttpStatusCode.Unauthorized,
            @"{""message"": ""Invalid API key""}");

        // Act & Assert
        await Assert.ThrowsAsync<AuthenticationException>(
            () => _client.Messages.ListBatchesAsync());
    }

    [Fact]
    public async Task ListBatchesAsync_With500Response_ThrowsSendlyException()
    {
        // Arrange
        for (int i = 0; i < 4; i++)
        {
            _mockHandler.QueueResponse(HttpStatusCode.InternalServerError,
                @"{""error"": ""Server error""}");
        }

        // Act & Assert
        await Assert.ThrowsAsync<SendlyException>(
            () => _client.Messages.ListBatchesAsync());
    }

    [Fact]
    public async Task ListBatchesAsync_WithEmptyResult_ReturnsEmptyList()
    {
        // Arrange
        var responseJson = @"{""data"": [], ""has_more"": false, ""total"": 0}";
        _mockHandler.QueueSuccessResponse(responseJson);

        // Act
        var result = await _client.Messages.ListBatchesAsync();

        // Assert
        Assert.Empty(result);
        Assert.False(result.HasMore);
        Assert.Equal(0, result.Total);
    }

    #endregion
}
