namespace GalgameManager.Contracts.Services;

public interface ILocalSettingsService
{
    Task<T?> ReadSettingAsync<T>(string key, bool isLarge = false);

    Task SaveSettingAsync<T>(string key, T value, bool isLarge = false);

    /// <summary>
    /// 获取远程存档根目录，若还未设置则弹出设置对话框
    /// </summary>
    /// <param name="reset">是否要重新选择目录</param>
    /// <returns>根目录地址，若用户取消设置则返回null</returns>
    Task<string?> GetRemoteFolder(bool reset = false);
}
