using Grpc.Core;
using Identity.Grpc.V1;

namespace OrdersService.Api.Services;

public sealed record CustomerInfo(Guid Id, string Email, string FirstName, string LastName);

// Application-facing wrapper around the generated gRPC client. Callers (e.g.
// OrdersController) depend on this small interface, not on Grpc.Core or the
// generated IdentityGrpcClient directly — keeps the gRPC plumbing swappable
// and easy to mock in tests.
public interface IIdentityUserClient
{
    Task<CustomerInfo?> GetUserAsync(Guid userId, CancellationToken cancellationToken);
    Task<IReadOnlyList<CustomerInfo>> GetUsersAsync(IEnumerable<Guid> userIds, CancellationToken cancellationToken);
}

public sealed class IdentityUserClient : IIdentityUserClient
{
    private readonly IdentityGrpc.IdentityGrpcClient _client;

    public IdentityUserClient(IdentityGrpc.IdentityGrpcClient client) => _client = client;

    public async Task<CustomerInfo?> GetUserAsync(Guid userId, CancellationToken cancellationToken)
    {
        var reply = await _client.GetUserAsync(
            new GetUserRequest { UserId = userId.ToString() },
            cancellationToken: cancellationToken);

        return reply.Found
            ? new CustomerInfo(Guid.Parse(reply.Id), reply.Email, reply.FirstName, reply.LastName)
            : null;
    }

    public async Task<IReadOnlyList<CustomerInfo>> GetUsersAsync(IEnumerable<Guid> userIds, CancellationToken cancellationToken)
    {
        var request = new GetUsersRequest();
        request.UserIds.AddRange(userIds.Select(id => id.ToString()));

        var reply = await _client.GetUsersAsync(request, cancellationToken: cancellationToken);

        return reply.Users
            .Where(u => u.Found)
            .Select(u => new CustomerInfo(Guid.Parse(u.Id), u.Email, u.FirstName, u.LastName))
            .ToList();
    }
}
