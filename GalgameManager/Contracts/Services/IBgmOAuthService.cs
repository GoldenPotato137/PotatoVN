namespace GalgameManager.Contracts.Services;

public interface IBgmOAuthService
{
    Task StartOAuth();
    Task FinishOAuthWithUri(string uri);
    
}