namespace GalgameManager.Contracts.Services;

public interface ILocalSettingsService
{
    Task<T?> ReadSettingAsync<T>(string key, bool isLarge = false);

    Task SaveSettingAsync<T>(string key, T value, bool isLarge = false, bool triggerEventWhenNull = false);

    Task RemoveSettingAsync(string key);
    
    public delegate void Delegate(string key, object? value);
    
    /// <summary>
    /// 当设置值改变时触发
    /// </summary>
    public event Delegate? OnSettingChanged;
}
