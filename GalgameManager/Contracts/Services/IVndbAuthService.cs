namespace GalgameManager.Contracts.Services;

public interface IVndbAuthService
{
    Task AuthWithToken(string token);
    
    Task LogoutAsync();
}