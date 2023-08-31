using GalgameManager.Enums;
using Newtonsoft.Json;
using SQLite;

namespace GalgameManager.Models;

[Table("commit")]
public class SyncCommit
{
    [PrimaryKey, AutoIncrement]
    public long Timestamp {get;set;}
    public CommitType Type {get;set;}
    public string Content {get;set;}
    
    public SyncCommit(CommitType type, object content)
    {
        Timestamp = DateTimeOffset.Now.ToUnixTimeSeconds();
        Type = type;
        Content = JsonConvert.SerializeObject(content);
    }
}