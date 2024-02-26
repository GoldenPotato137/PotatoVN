using GalgameManager.Server.Contracts;
using GalgameManager.Server.Enums;

namespace GalgameManager.Server.Models;

public class UserDto(User user)
{
    public int Id { get; set; } = user.Id;
    public string UserName { get; set; } = user.UserName;
    public string UserDisplayName { get; set; } = user.DisplayUserName;
    public UserType Type { get; set; } = user.Type;
    /// <summary>用户所属的BangumiId，若为0则表示未绑定</summary>>
    public int BangumiId { get; set; } = user.BangumiId;
    /// <summary>用户头像URL，可能为null</summary>>
    public string? Avatar { get; set; }
    public long UsedSpace { get; set; } = user.UsedSpace;
    public long TotalSpace { get; set; } = user.TotalSpace;

    public long LastGalChangedTimeStamp { get; set; } = user.LastGalChangedTimeStamp;

    public async Task WithAvatarAsync(IOssService ossService)
    {
        Avatar = await ossService.GetReadPresignedUrlAsync(user.Id, user.AvatarLoc);
    }
}

public class UserWithTokenDto (UserDto dto, string token, long expire)
{
    public UserDto User { get; set; } = dto;
    public string Token { get; set; } = token;
    public long Expire { get; set; } = expire;
}