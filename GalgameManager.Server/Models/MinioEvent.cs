using System.Text.Json.Serialization;

namespace GalgameManager.Server.Models;

public class MinioEvent
{
    [JsonPropertyName("EventName")]
    public required string EventName { get; set; }
    
    [JsonPropertyName("Records")]
    public List<Record> Records { get; set; } = [];
}

public class Record
{
    public required S3Entity S3 { get; set; }
}

public class S3Entity
{
    public required Bucket Bucket { get; set; }
    public required ObjectEntity Object { get; set; }
}

public class Bucket
{
    public required string Name { get; set; }
}

public class ObjectEntity
{
    public required string Key { get; set; }
    public long Size { get; set; }
}