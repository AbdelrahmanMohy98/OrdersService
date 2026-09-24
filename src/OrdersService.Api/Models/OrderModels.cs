namespace OrdersService.Api.Models;

// In-memory demo data — the point of this project is to show the auth +
// gRPC wiring, not a real order-persistence story.
public sealed record Order(Guid Id, Guid CustomerId, string Item, decimal Amount, DateTime PlacedOnUtc);

public sealed record OrderWithCustomerResponse(
    Guid Id,
    string Item,
    decimal Amount,
    DateTime PlacedOnUtc,
    string? CustomerEmail,
    string? CustomerName);
