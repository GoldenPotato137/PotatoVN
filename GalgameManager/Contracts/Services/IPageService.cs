namespace GalgameManager.Contracts.Services;

public interface IPageService
{
    public Action? OnInit
    {
        get;
        set;
    }
    
    Type GetPageType(string key);

    /// <summary>
    /// 初始化窗口，若已经初始化则什么也不做
    /// </summary>
    Task InitAsync();
}
