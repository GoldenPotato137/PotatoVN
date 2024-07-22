using GalgameManager.Enums;

namespace GalgameManager.Helpers;

public static class RssTypeHelper
{
    public static string? GetAbbr(this RssType rssType)
        => rssType switch
        {
            RssType.Vndb => "vndb",
            RssType.Bangumi => "bgm",
            RssType.PotatoVn => "pvn",
            RssType.Ymgal => "ymgal",
            _ => null
        };
    
    public static RssType? GetRssType(this string rssType)
        => rssType switch
        {
            "vndb" => RssType.Vndb,
            "bgm" => RssType.Bangumi,
            "pvn" => RssType.PotatoVn,
            "ymgal" => RssType.Ymgal,
            _ => null
        };
}