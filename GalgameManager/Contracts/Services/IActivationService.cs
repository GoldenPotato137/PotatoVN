namespace GalgameManager.Contracts.Services;

public interface IActivationService
{
    Task LaunchedAsync(object activationArgs);
}
