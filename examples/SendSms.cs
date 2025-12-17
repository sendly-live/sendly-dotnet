using Sendly;
using Sendly.Exceptions;

// Create client
using var client = new SendlyClient(
    Environment.GetEnvironmentVariable("SENDLY_API_KEY") ?? "sk_test_v1_example"
);

try
{
    // Send an SMS
    var message = await client.Messages.SendAsync(
        "+15551234567",
        "Hello from Sendly .NET SDK!"
    );

    Console.WriteLine("Message sent successfully!");
    Console.WriteLine($"  ID: {message.Id}");
    Console.WriteLine($"  To: {message.To}");
    Console.WriteLine($"  Status: {message.Status}");
    Console.WriteLine($"  Credits used: {message.CreditsUsed}");
}
catch (AuthenticationException e)
{
    Console.WriteLine($"Authentication failed: {e.Message}");
}
catch (InsufficientCreditsException e)
{
    Console.WriteLine($"Insufficient credits: {e.Message}");
}
catch (RateLimitException e)
{
    Console.WriteLine($"Rate limited. Retry after: {e.RetryAfter?.TotalSeconds} seconds");
}
catch (ValidationException e)
{
    Console.WriteLine($"Validation error: {e.Message}");
}
catch (SendlyException e)
{
    Console.WriteLine($"Error: {e.Message}");
}
