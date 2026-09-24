using System.Text;
using Grpc.Core;
using Identity.Grpc.V1;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using OrdersService.Api.Security;
using OrdersService.Api.Services;

// Plaintext HTTP/2 (h2c) to IdentityService's internal gRPC port is off by
// default in .NET — this is what turns it on. In production this call site
// is exactly where you'd instead point at an https:// address and drop this
// switch, once the internal network uses TLS (e.g. via a service mesh).
AppContext.SetSwitch("System.Net.Http.SocketsHttpHandler.Http2UnencryptedSupport", true);

var builder = WebApplication.CreateBuilder(args);

var jwtOptions = builder.Configuration.GetSection(JwtOptions.SectionName).Get<JwtOptions>()
                  ?? throw new InvalidOperationException("Jwt configuration section is missing.");

// Local, stateless validation against the signing key IdentityService also
// holds — this is the "auth" itself, and it costs zero network round trips
// per request. This is the standard pattern for resource services in a
// distributed system; only IdentityService needs to be called at login/refresh time.
builder.Services.AddAuthentication(options =>
    {
        options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
        options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
    })
    .AddJwtBearer(options =>
    {
        // Keep claim types as issued ("sub", not the long nameidentifier
        // URI) — see IdentityService.Infrastructure.DependencyInjection for
        // the full explanation. Must match on both services since they both
        // read the "sub" claim directly.
        options.MapInboundClaims = false;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwtOptions.Issuer,
            ValidAudience = jwtOptions.Audience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtOptions.SigningKey)),
            ClockSkew = TimeSpan.FromSeconds(30)
        };

        // ASP.NET Core returns a bare 401 with no detail on validation
        // failure by default, which makes "why am I unauthorized" nearly
        // impossible to answer from the client side. This logs the real
        // reason (expired token, signing-key mismatch, wrong issuer/audience,
        // missing header entirely...) to the console in Development.
        if (builder.Environment.IsDevelopment())
        {
            options.Events = new JwtBearerEvents
            {
                OnAuthenticationFailed = context =>
                {
                    Console.WriteLine($"[JWT] Authentication failed: {context.Exception.GetType().Name} - {context.Exception.Message}");
                    return Task.CompletedTask;
                },
                OnChallenge = context =>
                {
                    Console.WriteLine($"[JWT] Challenge issued (401). Error: {context.Error}, Description: {context.ErrorDescription}");
                    return Task.CompletedTask;
                }
            };
        }
    });
builder.Services.AddAuthorization();

builder.Services.Configure<IdentityGrpcOptions>(builder.Configuration.GetSection(IdentityGrpcOptions.SectionName));
var grpcOptions = builder.Configuration.GetSection(IdentityGrpcOptions.SectionName).Get<IdentityGrpcOptions>()
                   ?? throw new InvalidOperationException("IdentityGrpc configuration section is missing.");

// Typed gRPC client via Grpc.Net.ClientFactory: pooled/multiplexed HTTP/2
// connections handled for us, which is a large part of why gRPC calls between
// services end up faster in practice than opening a fresh HttpClient request
// per call — connections are reused instead of renegotiated each time.
builder.Services
    .AddGrpcClient<IdentityGrpc.IdentityGrpcClient>(options =>
    {
        options.Address = new Uri(grpcOptions.Address);
    })
    .ConfigureChannel(options =>
    {
        options.Credentials = ChannelCredentials.Create(
            ChannelCredentials.Insecure,
            InternalApiKeyCredentials.Create(grpcOptions.InternalApiKey));
    });

builder.Services.AddScoped<IIdentityUserClient, IdentityUserClient>();

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new Microsoft.OpenApi.Models.OpenApiInfo { Title = "Orders Service", Version = "v1" });

    // Adds the "Authorize" button in Swagger UI so you can paste an access
    // token (from IdentityService's /api/auth/login) and have it attached
    // as `Authorization: Bearer <token>` on every request made from the docs —
    // without this, Swagger has no field to enter a token into at all.
    options.AddSecurityDefinition("Bearer", new Microsoft.OpenApi.Models.OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = Microsoft.OpenApi.Models.SecuritySchemeType.Http,
        Scheme = "Bearer",
        BearerFormat = "JWT",
        In = Microsoft.OpenApi.Models.ParameterLocation.Header,
        Description = "Paste the accessToken from POST /api/auth/login on Identity Service (no need to type \"Bearer \" — Swagger adds that for you)."
    });
    options.AddSecurityRequirement(new Microsoft.OpenApi.Models.OpenApiSecurityRequirement
    {
        {
            new Microsoft.OpenApi.Models.OpenApiSecurityScheme
            {
                Reference = new Microsoft.OpenApi.Models.OpenApiReference
                {
                    Type = Microsoft.OpenApi.Models.ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            Array.Empty<string>()
        }
    });
});

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();

public partial class Program { }
