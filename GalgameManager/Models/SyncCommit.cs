using GalgameManager.Enums;
using Newtonsoft.Json;
using SQLite;

namespace GalgameManager.Models;

[Table("commit")]
public class SyncCommit
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }
    public long Timestamp {get;set;}
    public CommitType Type {get;set;}
    public string Content {get;set;}

    public SyncCommit()
    {
        Content = string.Empty;
    }
    
    public SyncCommit(CommitType type, object content)
    {
        Timestamp = DateTimeOffset.Now.ToUnixTimeSeconds();
        Type = type;
        Content = JsonConvert.SerializeObject(content);
    }
}

public class AddCommit
{
    public string name = string.Empty;
    public string bgmId = string.Empty;
}