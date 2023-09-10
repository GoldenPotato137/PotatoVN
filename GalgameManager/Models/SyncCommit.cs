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

    public string BgmId{get;set;} = string.Empty;

    public SyncCommit()
    {
        Content = string.Empty;
    }
    
    public SyncCommit(CommitType type, string bgmId, object content)
    {
        Timestamp = DateTimeOffset.Now.ToUnixTimeSeconds();
        Type = type;
        BgmId = bgmId;
        Content = JsonConvert.SerializeObject(content);
    }
}

public class AddCommit
{
    public string Name = string.Empty;
}

public class PlayCommit
{
    public string Date = string.Empty;
    public int Time;
}

public class DeleteCommit { }
