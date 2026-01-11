using CatalogService.Data;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using BuildingBlocks.Logging;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;

var builder = WebApplication.CreateBuilder(args);

// 1. One line configuration
builder.AddCustomLogging();

// Add services to the container.
builder.Services.AddDbContext<CatalogDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddStackExchangeRedisCache(options =>
{
    options.Configuration = builder.Configuration.GetConnectionString("Redis");
    options.InstanceName = builder.Configuration["Redis:InstanceName"] ?? "CatalogService_";
});

builder.Services.AddMassTransit(x =>
{
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
        Title = "Catalog Service API",
        Version = "v1",
        Description = "A production-ready ASP.NET Core Web API with Keycloak authentication for catalog management",
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

builder.Services.AddEndpointsApiExplorer();

builder.Services.AddControllers();

var app = builder.Build();

// Migrate Database
using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<CatalogDbContext>();
    // In production, use Migrate()
    dbContext.Database.EnsureCreated();

    // Seed Data
    DbInitializer.Initialize(dbContext);
}

// Configure the HTTP request pipeline
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "Catalog Service API v1");
        c.OAuthClientId(swaggerConfig["ClientId"] ?? "catalog-service");
        c.OAuthClientSecret(swaggerConfig["ClientSecret"]);
        c.OAuthUsePkce();
        c.DisplayRequestDuration();
    });
}

app.UseCors("AllowAll");
app.UseCustomLogging();
app.UseHttpsRedirection();

// Add Authentication & Authorization middleware
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();