using GalgameManager.Contracts.Phrase;

namespace GalgameManager.WinApp.Base.Contracts;

/// <summary>
/// 声明这个插件能够提供一个游戏搜刮器 <br/>
/// 请自行选择一个大于100的id作为RssType（强制类型转换为RssType）
/// </summary>
public interface IParserProvider
{
    /// <summary>
    /// 返回一个游戏搜刮器 <br/>
    /// 请注意：这个接口只会在插件加载时调用一次，因此请记住实例引用。如果你需要修改搜刮器配置，请直接修改这个实例
    /// </summary>
    /// <returns></returns>
    public IGalInfoPhraser GetPhraser();
    
    /// <summary>
    /// 搜刮目标名称，如getchu、dlsite
    /// </summary>
    public string ParserName { get; }
}