namespace GalgameManager.Contracts.Services;

public interface IBgmOAuthService
{
    Task StartOAuth();
    Task FinishOAuthWithUri(string uri);
    Task<int> CheckOAuthState();

    Task RefreshOAuthState();
}