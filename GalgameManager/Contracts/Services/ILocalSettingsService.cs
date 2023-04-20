namespace GalgameManager.Contracts.Services;

public interface ILocalSettingsService
{
    Task<T?> ReadSettingAsync<T>(string key, bool isLarge = false);

    Task SaveSettingAsync<T>(string key, T value, bool isLarge = false);
}
