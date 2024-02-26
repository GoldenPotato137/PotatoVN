using GalgameManager.Server.Models;

namespace GalgameManager.Server.Contracts;

public interface IOssService
{
    public string BucketName { get; }
    
    /// <summary>
    /// 每位用户的在oss上的空间大小，byte
    /// </summary>
    public long SpacePerUser { get; }
    
    public string OssEventToken { get; }
    
    /// <summary>
    /// 获取带有userId前缀的key, e.g. 114/1919/514.jpg (objectFullName = 1919/514.jpg)
    /// </summary>
    public string GetFullKey(int userId, string objectFullName);
    
    public Task<string?> GetWritePresignedUrlAsync(int userId, string objectFullName);
    
    public Task<string?> GetReadPresignedUrlAsync(int userId, string objectFullName);
    
    /// <summary>
    /// 需要在外部捕获异常
    /// </summary>
    public Task DeleteObjectAsync(int userId, string objectFullName);
    
    /// <summary>
    /// 更新用户的已使用空间，若key中没有合法的userId前缀/用户不存在则什么都不做
    /// </summary>
    /// <param name="entity">oss回调给的entity</param>
    public Task UpdateUserUsedSpaceAsync(ObjectEntity entity);
}