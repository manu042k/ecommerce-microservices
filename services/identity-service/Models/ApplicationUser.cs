namespace IdentityService.Models;

public class ApplicationUser
{
    public string Id { get; set; } = string.Empty; // Keycloak User ID (GUID)
    public string Username { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public bool EmailVerified { get; set; }
    public bool Enabled { get; set; }
    public Dictionary<string, List<string>> Attributes { get; set; } = new();
    public List<string> Roles { get; set; } = new();
}
