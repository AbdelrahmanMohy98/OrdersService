using Grpc.Core;

namespace OrdersService.Api.Security;

// Attaches the shared internal API key to every outgoing gRPC call as
// metadata, matching the key IdentityService's InternalApiKeyInterceptor
// checks for. This is what authenticates OrdersService *as a caller* to
// IdentityService — separate from the end user's own JWT, which OrdersService
// validates independently for its own inbound requests.
//
// CallCredentials (from Grpc.Core.Api, used by Grpc.Net.Client) isn't meant
// to be subclassed directly — its abstract members are internal-only. The
// supported extension point is the FromInterceptor factory.
public static class InternalApiKeyCredentials
{
    public static CallCredentials Create(string apiKey) =>
        CallCredentials.FromInterceptor((_, metadata) =>
        {
            metadata.Add("x-internal-api-key", apiKey);
            return Task.CompletedTask;
        });
}
