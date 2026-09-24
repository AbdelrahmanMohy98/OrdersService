# Orders Service (example consumer of Identity Service)

A minimal service demonstrating how a downstream microservice authenticates
users and talks to the Identity Service in a distributed system. Domain logic
(orders) is intentionally trivial — the point of this project is the auth +
gRPC wiring, which is what generalizes to any real service.

## Two different problems, two different mechanisms

**1. "Is this incoming request from a logged-in user?"** — validated
**locally**, via `AddJwtBearer`, against the same signing key Identity Service
uses to issue tokens. No network call to Identity Service happens on a normal
request. This is deliberate: per-request auth calls to another service would
add latency to *every single request* this service handles and make Identity
Service a hard dependency for this service staying up at all. Stateless JWT
validation is the standard, fast pattern for this.

**2. "I need fresh data about a specific user"** — this is where gRPC comes
in (`OrdersController.GetWithCustomer`). When OrdersService needs current
profile data (email, name) for a customer — not just whatever was in the JWT
claims at login time — it calls Identity Service's internal `IdentityGrpc`
service. gRPC is the right tool here specifically because it's a genuine
service-to-service call: binary protobuf serialization and HTTP/2 connection
reuse make it noticeably faster than an equivalent REST/JSON call, and the
shared `.proto` contract keeps both sides in sync without hand-written DTOs.

Calling gRPC (or anything) on *every* incoming request just to check the
token would defeat the purpose of using JWTs at all — that's the mistake this
design avoids.

## Internal gRPC auth

Every outgoing gRPC call carries an `x-internal-api-key` metadata header
(`Security/InternalApiKeyCredentials.cs`), checked by Identity Service's
`InternalApiKeyInterceptor`. This authenticates OrdersService *as a calling
service*, separate from whichever end user's data it's asking about. Good
enough behind a private network/VPC; swap for mTLS if the network boundary
itself isn't trusted.

## Configuration

`appsettings.json`:
- `Jwt:*` — **must exactly match** Identity Service's `Jwt` section (issuer,
  audience, signing key). In practice, source both services' secrets from the
  same place (shared vault/parameter store), not two copies of a config file.
- `IdentityGrpc:Address` — Identity Service's gRPC port (`5001` by default).
- `IdentityGrpc:InternalApiKey` — **must exactly match** Identity Service's
  `Grpc:InternalApiKey`.

## Running both services together

```bash
docker compose up --build
```

This starts SQL Server, Identity Service (REST on 5000, gRPC on 5001), and
Orders Service (8080) — see `docker-compose.yml`.

Manual flow:
```bash
# 1. Register + login against Identity Service to get an access token
curl -X POST http://localhost:5000/api/auth/register -H "Content-Type: application/json" \
  -d '{"email":"a@b.com","password":"Passw0rd!","firstName":"Ada","lastName":"Lovelace"}'
curl -X POST http://localhost:5000/api/auth/login -H "Content-Type: application/json" \
  -d '{"email":"a@b.com","password":"Passw0rd!"}'
# -> copy accessToken from the response

# 2. Call Orders Service with that token
curl -X POST http://localhost:8080/api/orders -H "Authorization: Bearer <token>" \
  -H "Content-Type: application/json" -d '{"item":"Widget","amount":9.99}'

# 3. Fetch it back enriched with live customer data pulled over gRPC
curl http://localhost:8080/api/orders/<order-id>/with-customer -H "Authorization: Bearer <token>"
```

Step 3 is the one that actually goes over gRPC to Identity Service; steps 1-2
never do.
