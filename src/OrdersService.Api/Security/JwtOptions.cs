namespace OrdersService.Api.Security;

// Mirrors IdentityService.Infrastructure.Security.JwtOptions exactly: both
// services must agree on Issuer/Audience/SigningKey since OrdersService
// validates tokens *without* calling IdentityService — that's the whole
// point of using stateless JWTs instead of a network round trip per request.
public sealed class JwtOptions
{
    public const string SectionName = "Jwt";

    public string Issuer { get; init; } = default!;
    public string Audience { get; init; } = default!;
    public string SigningKey { get; init; } = default!;
}
