using System.Security.Claims;
using System.Text.Json;
using BuildingBlocks.Logging;
using MassTransit;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using PaymentService.Data;
using PaymentService.Services;
using PaymentService.Services.Providers;

var builder = WebApplication.CreateBuilder(args);

builder.AddCustomLogging();

builder.Services.AddDbContext<PaymentDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddStackExchangeRedisCache(options =>
{
    options.Configuration = builder.Configuration.GetConnectionString("Redis");
    options.InstanceName = builder.Configuration["Redis:InstanceName"] ?? "PaymentService_";
});

builder.Services.AddMassTransit(x =>
{
    x.SetKebabCaseEndpointNameFormatter();
    x.UsingRabbitMq((context, cfg) =>
    {
        var host = builder.Configuration["RabbitMQ:Host"] ?? "rabbitmq";
        var userName = builder.Configuration["RabbitMQ:UserName"] ?? "guest";
        var password = builder.Configuration["RabbitMQ:Password"] ?? "guest";

        cfg.Host(host, "/", h =>
        {
            h.Username(userName);
            h.Password(password);
        });

        cfg.ConfigureEndpoints(context);
    });
});

builder.Services.Configure<StripeOptions>(builder.Configuration.GetSection("PaymentProviders:Stripe"));
builder.Services.AddSingleton<IPaymentProvider, StripePaymentProvider>();
builder.Services.AddScoped<IPaymentService, PaymentService.Services.PaymentService>();

var keycloakConfig = builder.Configuration.GetSection("Keycloak");

builder.Services.AddAuthentication(options =>
    {
        options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
        options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
    })
    .AddJwtBearer(options =>
    {
        options.Authority = $"{keycloakConfig["AuthServerUrl"]}realms/{keycloakConfig["Realm"]}" ?? throw new InvalidOperationException("Keycloak Authority not configured");
        options.Audience = keycloakConfig["Resource"] ?? "account";
        options.MetadataAddress = $"{keycloakConfig["AuthServerUrl"]}realms/{keycloakConfig["Realm"]}/.well-known/openid_configuration";
        options.RequireHttpsMetadata = bool.Parse(keycloakConfig["RequireHttpsMetadata"] ?? "false");

        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = false,
            ValidateAudience = false,
            ValidateLifetime = false,
            ValidateIssuerSigningKey = false,
            RequireAudience = false,
            RequireExpirationTime = false,
            RequireSignedTokens = false,
            SignatureValidator = (token, _) => new JsonWebToken(token),
            ClockSkew = TimeSpan.Zero,
            NameClaimType = ClaimTypes.NameIdentifier,
            RoleClaimType = ClaimTypes.Role
        };

        options.Events = new JwtBearerEvents
        {
            OnTokenValidated = context =>
            {
                if (context.Principal?.Identity is ClaimsIdentity identity)
                {
                    var realmAccess = context.Principal.FindFirst("realm_access")?.Value;
                    if (!string.IsNullOrEmpty(realmAccess))
                    {
                        try
                        {
                            using var document = JsonDocument.Parse(realmAccess);
                            if (document.RootElement.TryGetProperty("roles", out var rolesElement))
                            {
                                foreach (var role in rolesElement.EnumerateArray())
                                {
                                    var roleName = role.GetString();
                                    if (!string.IsNullOrEmpty(roleName) && !identity.HasClaim(identity.RoleClaimType, roleName))
                                    {
                                        identity.AddClaim(new Claim(identity.RoleClaimType, roleName));
                                    }
                                }
                            }
                        }
                        catch (JsonException ex)
                        {
                            var logger = context.HttpContext.RequestServices.GetRequiredService<ILoggerFactory>().CreateLogger("PaymentAuth");
                            logger.LogWarning(ex, "Failed to parse realm_access claims");
                        }
                    }
                }

                return Task.CompletedTask;
            }
        };
    });

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("CustomersOrAdmin", policy => policy.RequireRole("Customer", "User", "Admin"));
    options.AddPolicy("FinanceOrAdmin", policy => policy.RequireRole("Finance", "Admin"));
});

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyHeader()
              .AllowAnyMethod();
    });
});

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "Payment Service API",
        Version = "v1",
        Description = "Handles payment intents, refunds, and provider webhooks"
    });

    c.AddSecurityDefinition("oauth2", new OpenApiSecurityScheme
    {
        Type = SecuritySchemeType.OAuth2,
        Flows = new OpenApiOAuthFlows
        {
            AuthorizationCode = new OpenApiOAuthFlow
            {
                AuthorizationUrl = new Uri(builder.Configuration["Swagger:AuthorizationUrl"] ?? "http://localhost:8080/realms/ecommerce-realm/protocol/openid-connect/auth"),
                TokenUrl = new Uri(builder.Configuration["Swagger:TokenUrl"] ?? "http://localhost:8080/realms/ecommerce-realm/protocol/openid-connect/token"),
                Scopes = new Dictionary<string, string>
                {
                    { "openid", "OpenID" },
                    { "profile", "Profile" },
                    { "email", "Email" }
                }
            }
        }
    });

    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "oauth2"
                }
            },
            new[] { "openid", "profile", "email" }
        }
    });
});

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<PaymentDbContext>();
    await DbInitializer.InitializeAsync(dbContext);
}

app.UseCustomLogging();
app.UseCors("AllowAll");

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "Payment Service API v1");
        c.OAuthClientId(builder.Configuration["Swagger:ClientId"] ?? "payment-service");
        c.OAuthClientSecret(builder.Configuration["Swagger:ClientSecret"]);
        c.OAuthUsePkce();
        c.DisplayRequestDuration();
    });
}

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();

public partial class Program
{
}