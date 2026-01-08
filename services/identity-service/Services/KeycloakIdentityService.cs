using System.Net.Http.Headers;
using System.Text.Json;
using IdentityService.Dtos;
using Microsoft.Extensions.Configuration;

namespace IdentityService.Services;

public class KeycloakIdentityService : IIdentityService
{
    private readonly HttpClient _httpClient;
    private readonly IConfiguration _configuration;
    private readonly ILogger<KeycloakIdentityService> _logger;

    public KeycloakIdentityService(HttpClient httpClient, IConfiguration configuration, ILogger<KeycloakIdentityService> logger)
    {
        _httpClient = httpClient;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task<AuthResponse?> LoginAsync(LoginRequest request)
    {
        var authServerUrl = _configuration["Keycloak:AuthServerUrl"];
        var realm = _configuration["Keycloak:Realm"];
        var clientId = _configuration["Keycloak:Resource"];
        var clientSecret = _configuration["Keycloak:Credentials:Secret"];

        // Token Endpoint
        var tokenUrl = $"{authServerUrl}realms/{realm}/protocol/openid-connect/token";

        var keyValues = new List<KeyValuePair<string, string>>
        {
            new("client_id", clientId!),
            new("client_secret", clientSecret!),
            new("grant_type", "password"),
            new("username", request.Username),
            new("password", request.Password)
        };

        var content = new FormUrlEncodedContent(keyValues);

        var response = await _httpClient.PostAsync(tokenUrl, content);

        if (!response.IsSuccessStatusCode)
        {
            var errorContent = await response.Content.ReadAsStringAsync();
            _logger.LogWarning("Keycloak login failed: {Error}", errorContent);
            return null;
        }

        var responseString = await response.Content.ReadAsStringAsync();
        return JsonSerializer.Deserialize<AuthResponse>(responseString);
    }

    public async Task<bool> RegisterAsync(RegisterRequest request)
    {
        // Registration is usually done via Admin API (requiring admin token) 
        // OR simply create a user via Admin API.
        // For simplicity, we'll try to get an Admin Token first, then create the user.

        var adminToken = await GetAdminAccessTokenAsync();
        if (string.IsNullOrEmpty(adminToken)) return false;

        var authServerUrl = _configuration["Keycloak:AuthServerUrl"];
        var realm = _configuration["Keycloak:Realm"];

        var createUserUrl = $"{authServerUrl}admin/realms/{realm}/users";

        var user = new
        {
            username = request.Username,
            email = request.Email,
            firstName = request.FirstName,
            lastName = request.LastName,
            enabled = true,
            emailVerified = false,
            credentials = new[]
            {
                new { type = "password", value = request.Password, temporary = false }
            }
        };

        var requestMessage = new HttpRequestMessage(HttpMethod.Post, createUserUrl);
        requestMessage.Headers.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);
        requestMessage.Content = JsonContent.Create(user);

        var response = await _httpClient.SendAsync(requestMessage);

        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadAsStringAsync();
            _logger.LogError("Failed to register user in Keycloak: {Error}", error);
            return false;
        }

        return true;
    }

    private async Task<string?> GetAdminAccessTokenAsync()
    {
        var authServerUrl = _configuration["Keycloak:AuthServerUrl"];
        var realm = _configuration["Keycloak:Realm"];
        // Usually admin-cli or a dedicated service client with management roles
        var adminClientId = _configuration["Keycloak:AdminClientId"] ?? "admin-cli";
        var adminClientSecret = _configuration["Keycloak:AdminClientSecret"]; // May not be needed for public client, but 'admin-cli' is usually public only if confident. Using a service account is better.

        // Assuming we are using a service account client with realm-management roles
        var tokenUrl = $"{authServerUrl}realms/master/protocol/openid-connect/token";
        // Note: Admin operations often need to auth against 'master' realm OR the specific realm depending on setup. 
        // If we use a client inside 'ecommerce-realm' that has 'realm-management' roles, we auth against 'ecommerce-realm'.
        // Let's assume a client in the target realm with service-account roles.

        // RE-EVALUATION: To create users in 'ecommerce-realm', we need a token from a client that has permissions.
        // Let's assume we use the same client 'identity-client' and it has 'service accounts enabled' and 'realm-admin' roles or similar.
        tokenUrl = $"{authServerUrl}realms/{realm}/protocol/openid-connect/token";

        var clientId = _configuration["Keycloak:Resource"];
        var clientSecret = _configuration["Keycloak:Credentials:Secret"];

        var keyValues = new List<KeyValuePair<string, string>>
        {
            new("client_id", clientId!),
            new("client_secret", clientSecret!),
            new("grant_type", "client_credentials")
        };

        var content = new FormUrlEncodedContent(keyValues);
        var response = await _httpClient.PostAsync(tokenUrl, content);

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogError("Failed to get admin token");
            return null;
        }

        var responseString = await response.Content.ReadAsStringAsync();
        var tokenResponse = JsonSerializer.Deserialize<AuthResponse>(responseString);
        return tokenResponse?.AccessToken;
    }
}
