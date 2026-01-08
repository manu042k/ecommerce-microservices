using IdentityService.Dtos;

namespace IdentityService.Services;

public interface IIdentityService
{
    Task<AuthResponse?> LoginAsync(LoginRequest request);
    Task<bool> RegisterAsync(RegisterRequest request);
}
