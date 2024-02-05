using GalgameManager.Server.Contracts;
using Minio;
using Minio.DataModel.Args;
using Minio.Exceptions;

namespace GalgameManager.Server.Services;

public class OssService (IMinioClient client, IConfiguration config) : IOssService
{
    private readonly string _bucketName = config["AppSettings:Minio:BucketName"] ?? "potatovn";
    
    public async Task<string?> GetWritePresignedUrlAsync(int userId, string objectFullName)
    {
        try
        {
            return await client.PresignedPutObjectAsync(new PresignedPutObjectArgs()
                .WithBucket(_bucketName)
                .WithObject($"{userId}/{objectFullName}")
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
        try
        {
            return await client.PresignedGetObjectAsync(new PresignedGetObjectArgs()
                .WithBucket(_bucketName)
                .WithObject($"{userId}/{objectFullName}")
                .WithExpiry(10 * 60));
        }
        catch (Exception e)
        {
            if (e is InvalidObjectNameException or ObjectNotFoundException)
                return null;
            throw;
        }
    }
}