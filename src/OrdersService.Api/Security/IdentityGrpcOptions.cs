namespace OrdersService.Api.Security;

public sealed class IdentityGrpcOptions
{
    public const string SectionName = "IdentityGrpc";

    public string Address { get; init; } = default!;   // e.g. http://identity-service:5001
    public string InternalApiKey { get; init; } = default!;
}
