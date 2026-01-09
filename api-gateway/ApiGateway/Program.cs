using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddCors(options =>
{
    options.AddPolicy("CorsPolicy",
        b => b.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader());
});

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        var keycloakSettings = builder.Configuration.GetSection("Keycloak");
        options.Authority = $"{keycloakSettings["AuthServerUrl"]}realms/{keycloakSettings["Realm"]}";
        options.Audience = keycloakSettings["Resource"];
        options.RequireHttpsMetadata = false;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateAudience = true,
            ValidAudience = keycloakSettings["Resource"],
            ValidateIssuer = false,
            ValidIssuer = $"{keycloakSettings["AuthServerUrl"]}realms/{keycloakSettings["Realm"]}",
            ValidateLifetime = true
        };
    });

// Add YARP
builder.Services.AddReverseProxy()
    .LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"));

// Add Swagger services (for the UI shell)
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Description = "JWT Authorization header using the Bearer scheme. Enter 'Bearer' [space] and then your token in the text input below.",
        Name = "Authorization",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.ApiKey,
        Scheme = "Bearer"
    });

    // Add OAuth2 Definition (Redirect Flow)
    c.AddSecurityDefinition("OAuth2", new OpenApiSecurityScheme
    {
        Type = SecuritySchemeType.OAuth2,
        Flows = new OpenApiOAuthFlows
        {
            AuthorizationCode = new OpenApiOAuthFlow
            {
                AuthorizationUrl = new Uri("http://localhost:8080/realms/ecommerce-realm/protocol/openid-connect/auth"),
                TokenUrl = new Uri("http://localhost:8080/realms/ecommerce-realm/protocol/openid-connect/token"),
                Scopes = new Dictionary<string, string>
                {
                    { "openid", "OpenID Connect" },
                    { "profile", "User Profile" }
                }
            }
        }
    });

    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        // Allow Bearer Token
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" }
            },
            Array.Empty<string>()
        },
        // Allow OAuth2 Flow
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "OAuth2" }
            },
            new[] { "openid", "profile" }
        }
    });
});


var app = builder.Build();

app.UseCors("CorsPolicy");

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        // This configures the Gateway to show the Identity Service's docs
        // The path '/doc/identity/swagger.json' is a route we define in YARP (appsettings)
        c.SwaggerEndpoint("/doc/identity/swagger.json", "Identity Service");

        // OAuth Client Config for the Redirect Flow
        // We load these from configuration (which comes from Env vars in docker-compose)
        c.OAuthClientId(builder.Configuration["Keycloak:Resource"]);
        c.OAuthClientSecret(builder.Configuration["Keycloak:Credentials:Secret"]);
        c.OAuthAppName("Ecommerce API Gateway");
        c.OAuthUsePkce();
    });
}

app.UseAuthentication();
app.UseAuthorization();

// Enable YARP
app.MapReverseProxy();

app.Run();
