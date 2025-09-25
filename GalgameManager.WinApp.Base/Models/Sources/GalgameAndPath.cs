using System;
using GalgameManager.Enums;
using LiteDB;
using Newtonsoft.Json;

namespace GalgameManager.Models.Sources;

public class GalgameAndPath(Galgame game, string path)
{
    public Galgame Galgame { get; set; } = game;
    public string Path { get; set; } = path;
    [JsonIgnore][BsonIgnore] 
    public RssType[] RssTypes { get; } = [RssType.Bangumi, RssType.Vndb, RssType.Ymgal, RssType.Cngal, RssType.Mixed, RssType.None];

    // 提供用于排序的原生类型快照，避免 ACV 比较 LockableProperty 时出错
    [JsonIgnore][BsonIgnore]
    public string NameForSort => Galgame.Name.Value ?? string.Empty;
    [JsonIgnore][BsonIgnore]
    public DateTime LastPlayTimeForSort => Galgame.LastPlayTime;
    [JsonIgnore][BsonIgnore]
    public string DeveloperForSort => Galgame.Developer.Value ?? string.Empty;
    [JsonIgnore][BsonIgnore]
    public float RatingForSort => Galgame.Rating is { } r && r.Value is float rv ? rv : 0f;
    [JsonIgnore][BsonIgnore]
    public DateTime ReleaseDateForSort => Galgame.ReleaseDate is { } d && d.Value is DateTime dv ? dv : DateTime.MinValue;
    [JsonIgnore][BsonIgnore]
    public DateTime AddTimeForSort => Galgame.AddTime;
    [JsonIgnore][BsonIgnore]
    public string PathForSort => Path ?? string.Empty;
}

public class GalgameAndPathDbDto
{
    public Guid GalgameId { get; set; }
    public string Path { get; set; } = string.Empty;
    
    public GalgameAndPathDbDto(Guid id, string path)
    {
        GalgameId = id;
        Path = path;
    }

    public GalgameAndPathDbDto() { }    
}