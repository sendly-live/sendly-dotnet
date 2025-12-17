using Sendly;
using Sendly.Models;

using var client = new SendlyClient(
    Environment.GetEnvironmentVariable("SENDLY_API_KEY") ?? "sk_test_v1_example"
);

// List recent messages
Console.WriteLine("=== Recent Messages ===");
var messages = await client.Messages.ListAsync(new ListMessagesOptions { Limit = 10 });

Console.WriteLine($"Total: {messages.Total}");
Console.WriteLine($"Has more: {messages.HasMore}");
Console.WriteLine();

foreach (var msg in messages)
{
    Console.WriteLine($"{msg.Id}: {msg.To} - {msg.Status}");
}

// List with filters
Console.WriteLine("\n=== Delivered Messages ===");
var delivered = await client.Messages.ListAsync(new ListMessagesOptions
{
    Status = "delivered",
    Limit = 5
});

foreach (var msg in delivered)
{
    Console.WriteLine($"{msg.Id}: Delivered at {msg.DeliveredAt}");
}

// Iterate all with auto-pagination
Console.WriteLine("\n=== All Messages (paginated) ===");
var count = 0;
await foreach (var msg in client.Messages.GetAllAsync())
{
    Console.WriteLine($"{msg.Id}: {msg.To}");
    count++;
    if (count >= 20) break; // Limit for demo
}
