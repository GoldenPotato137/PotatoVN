using GalgameManager.Server.Contracts;
using GalgameManager.Server.Models;
using Minio;
using Minio.DataModel.Args;
using Minio.Exceptions;

namespace GalgameManager.Server.Services;

public class OssService(
    IMinioClient client,
    IConfiguration config,
    IUserRepository userRepository,
    IOssRecordRepository ossRecordRepository) : IOssService
{
    public string BucketName { get; } = config["AppSettings:Minio:BucketName"] ?? "potatovn";

    public long SpacePerUser { get; } = config["AppSettings:User:OssSize"] is null
        ? 104857600 // 100MB
        : Convert.ToInt64(config["AppSettings:User:OssSize"]);

    public string OssEventToken { get; } = config["AppSettings:Minio:EventToken"]!;

    public string GetFullKey(int userId, string objectFullName) => $"{userId}/{objectFullName}";

    public async Task<string?> GetWritePresignedUrlAsync(int userId, string objectFullName)
    {
        if (string.IsNullOrEmpty(objectFullName)) return null;
        try
        {
            return await client.PresignedPutObjectAsync(new PresignedPutObjectArgs()
                .WithBucket(BucketName)
                .WithObject(GetFullKey(userId, objectFullName))
                .WithExpiry(10 * 60));
        }
        catch (Exception e)
        {
            if (e is InvalidObjectNameException)
                return null;
            throw;
        }
    }

    public async Task<string?> GetReadPresignedUrlAsync(int userId, string objectFullName)
    {
        if (string.IsNullOrEmpty(objectFullName)) return null;
        try
        {
            return await client.PresignedGetObjectAsync(new PresignedGetObjectArgs()
                .WithBucket(BucketName)
                .WithObject(GetFullKey(userId, objectFullName))
                .WithExpiry(10 * 60));
        }
        catch (Exception e)
        {
            if (e is InvalidObjectNameException or ObjectNotFoundException)
                return null;
            throw;
        }
    }

    public Task DeleteObjectAsync(int userId, string objectFullName)
    {
        return client.RemoveObjectAsync(new RemoveObjectArgs().WithBucket(BucketName)
            .WithObject(GetFullKey(userId, objectFullName)));
    }

    public async Task UpdateUserUsedSpaceAsync(ObjectEntity entity)
    {
        if (entity.Key.Contains('/') == false) return;
        var userId = Convert.ToInt32(entity.Key.Split('/')[0]);
        User? user = await userRepository.GetUserAsync(userId);
        if (user is null) return;
        
        user.UsedSpace += await ossRecordRepository.UpdateRecordAsync(user.Id, entity.Key, entity.Size);
        await userRepository.UpdateUserAsync(user);
    }
}