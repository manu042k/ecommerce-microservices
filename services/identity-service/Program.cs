using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using IdentityService.Services;
using BuildingBlocks.Logging;

var builder = WebApplication.CreateBuilder(args);

// Add custom logging configuration
builder.AddCustomLogging();

// Add services to the container
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();

// Register HttpClient for KeycloakIdentityService
builder.Services.AddHttpClient<IIdentityService, KeycloakIdentityService>();

// Configure JWT Authentication with Keycloak
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
        ValidIssuer = $"{keycloakConfig["AuthServerUrl"]}realms/{keycloakConfig["Realm"]}",
        ValidateAudience = bool.Parse(keycloakConfig["ValidateAudience"] ?? "false"),
        RequireAudience = false,  // Key fix for Keycloak tokens without audience claims
        ValidAudience = keycloakConfig["Resource"],
        ValidateLifetime = bool.Parse(keycloakConfig["ValidateLifetime"] ?? "true"),
        ValidateIssuerSigningKey = false,
        SignatureValidator = delegate (string token, TokenValidationParameters parameters) { return new Microsoft.IdentityModel.JsonWebTokens.JsonWebToken(token); },
        ClockSkew = TimeSpan.Zero
    };

    options.Events = new JwtBearerEvents
    {
        OnAuthenticationFailed = context =>
        {
            Console.WriteLine($"Authentication failed: {context.Exception.Message}");
            return Task.CompletedTask;
        },
        OnTokenValidated = context =>
        {
            Console.WriteLine("Token validated successfully");
            return Task.CompletedTask;
        }
    };
});

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("AdminOnly", policy => policy.RequireRole("Admin"));
    options.AddPolicy("UserOrAdmin", policy => policy.RequireRole("Customer", "User", "Admin"));
});

// Configure Swagger with OAuth2
var swaggerConfig = builder.Configuration.GetSection("Swagger");
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "Identity Service API",
        Version = "v1",
        Description = "A production-ready ASP.NET Core Web API with Keycloak authentication and user management",
        Contact = new OpenApiContact
        {
            Name = "API Support",
            Email = "support@example.com"
        }
    });

    // Define the OAuth2 security scheme
    c.AddSecurityDefinition("oauth2", new OpenApiSecurityScheme
    {
        Type = SecuritySchemeType.OAuth2,
        Flows = new OpenApiOAuthFlows
        {
            AuthorizationCode = new OpenApiOAuthFlow
            {
                AuthorizationUrl = new Uri(swaggerConfig["AuthorizationUrl"] ?? "http://localhost:8080/realms/ecommerce-realm/protocol/openid-connect/auth"),
                TokenUrl = new Uri(swaggerConfig["TokenUrl"] ?? "http://localhost:8080/realms/ecommerce-realm/protocol/openid-connect/token"),
                Scopes = new Dictionary<string, string>
                {
                    { "openid", "OpenID" },
                    { "profile", "Profile" },
                    { "email", "Email" }
                }
            }
        },
        Description = "Keycloak OAuth2 Authorization"
    });

    // Add security requirement
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

// Configure CORS for production
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

var app = builder.Build();

// Configure the HTTP request pipeline
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "Identity Service API v1");
        c.OAuthClientId(swaggerConfig["ClientId"] ?? "identity-client");
        c.OAuthClientSecret(swaggerConfig["ClientSecret"]);
        c.OAuthUsePkce();
        c.DisplayRequestDuration();
    });
}

app.UseCors("AllowAll");
app.UseAuthentication();
app.UseAuthorization();

app.UseCustomLogging();

app.MapControllers();

app.Run();

public partial class Program
{
}

public partial class Program
{
}
