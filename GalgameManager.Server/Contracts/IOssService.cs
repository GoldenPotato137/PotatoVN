namespace GalgameManager.Server.Contracts;

public interface IOssService
{
    public Task<string?> GetWritePresignedUrlAsync(int userId, string objectFullName);
    
    public Task<string?> GetReadPresignedUrlAsync(int userId, string objectFullName);
}