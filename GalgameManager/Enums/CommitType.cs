namespace GalgameManager.Enums;

public enum CommitType
{
    /// 添加游戏
    Add,
    
    /// 游玩游戏
    Play,
    
    /// 修改游戏状态（状态/评论/评分）
    ChangePlayType,
    
    /// 取消托管游戏
    Delete
}