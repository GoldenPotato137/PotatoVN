using System.Collections.Generic;
using GalgameManager.Enums;

namespace GalgameManager.Helpers;

public static class RssTypeHelper
{
    public static List<RssType> UsablePhrasers { get; } = [RssType.Bangumi, RssType.Vndb, RssType.Ymgal, RssType.Steam];
    
    public static string? GetAbbr(this RssType rssType)
        => rssType switch
        {
            RssType.Vndb => "vndb",
            RssType.Bangumi => "bgm",
            RssType.PotatoVn => "pvn",
            RssType.Ymgal => "ymgal",
            RssType.Steam => "steam",
            _ => null
        };
    
    public static RssType? GetRssType(this string rssType)
        => rssType switch
        {
            "vndb" => RssType.Vndb,
            "bgm" => RssType.Bangumi,
            "pvn" => RssType.PotatoVn,
            "ymgal" => RssType.Ymgal,
            "steam" => RssType.Steam,
            _ => null
        };
}