using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OrdersService.Api.Models;
using OrdersService.Api.Services;

namespace OrdersService.Api.Controllers;

[ApiController]
[Route("api/orders")]
[Authorize] // validated locally against IdentityService's signing key — no network call per request
public sealed class OrdersController : ControllerBase
{
    // Seeded in-memory so the endpoint has something to return; swap for a
    // real repository the same way IdentityService's UserRepository works.
    private static readonly List<Order> Seed = new();

    private readonly IIdentityUserClient _identityClient;

    public OrdersController(IIdentityUserClient identityClient) => _identityClient = identityClient;

    // Creates a demo order for the *caller* (taken from the validated JWT's
    // "sub" claim) — shows that authentication itself never touched the network.
    [HttpPost]
    public IActionResult Create([FromBody] CreateOrderRequest request)
    {
        var customerId = Guid.Parse(User.FindFirstValue("sub")!);
        var order = new Order(Guid.NewGuid(), customerId, request.Item, request.Amount, DateTime.UtcNow);
        Seed.Add(order);
        return Ok(order);
    }

    [HttpGet]
    public IActionResult GetMine()
    {
        var customerId = Guid.Parse(User.FindFirstValue("sub")!);
        return Ok(Seed.Where(o => o.CustomerId == customerId));
    }

    // Demonstrates the actual gRPC call: enriches an order with fresh
    // customer profile data pulled from IdentityService over gRPC, rather
    // than trusting whatever name/email happened to be in the JWT at login
    // time (which could be stale by the time this request comes in).
    [HttpGet("{id:guid}/with-customer")]
    public async Task<IActionResult> GetWithCustomer(Guid id, CancellationToken cancellationToken)
    {
        var order = Seed.FirstOrDefault(o => o.Id == id);
        if (order is null) return NotFound();

        var customer = await _identityClient.GetUserAsync(order.CustomerId, cancellationToken);

        return Ok(new OrderWithCustomerResponse(
            order.Id,
            order.Item,
            order.Amount,
            order.PlacedOnUtc,
            customer?.Email,
            customer is null ? null : $"{customer.FirstName} {customer.LastName}"));
    }
}

public sealed record CreateOrderRequest(string Item, decimal Amount);
