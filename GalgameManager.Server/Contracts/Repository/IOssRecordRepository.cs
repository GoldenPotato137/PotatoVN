using GalgameManager.Server.Models;

namespace GalgameManager.Server.Contracts;

public interface IOssRecordRepository
{
    /// <summary>
    /// 获取oss记录，如果不存在则返回null
    /// </summary>
    /// <param name="key">包括用户id前缀的完整key</param>
    public Task<OssRecord?> GetRecordByKeyAsync(string key);
    
    /// <summary>
    /// 更新用户的oss记录，如果不存在则创建，若size为0则删除记录
    /// </summary>
    /// <param name="userId"></param>
    /// <param name="key">包括用户id前缀的完整的key</param>
    /// <param name="size"></param>
    /// <returns>用户占用空间增量, byte</returns>
    public Task<long> UpdateRecordAsync(int userId, string key, long size);
}