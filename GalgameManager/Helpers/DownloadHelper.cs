using Windows.Storage;

namespace GalgameManager.Helpers;

public static class DownloadHelper
{
    /// <summary>
    /// 从网络下载图片并保存到本地
    /// </summary>
    /// <param name="imageUrl">图片链接</param>
    /// <returns>本地文件路径, 如果下载失败则返回null</returns>
    public static async Task<string?> DownloadAndSaveImageAsync(string? imageUrl)
    {
        try
        {
            if (imageUrl == null) return null;
            HttpClient httpClient = new();
            HttpResponseMessage response = await httpClient.GetAsync(imageUrl);
            response.EnsureSuccessStatusCode();

            var imageBytes = await response.Content.ReadAsByteArrayAsync();

            StorageFolder? localFolder = ApplicationData.Current.LocalFolder;
            var fileName = imageUrl[(imageUrl.LastIndexOf('/') + 1)..];
            StorageFile? storageFile = await localFolder.CreateFileAsync(fileName, CreationCollisionOption.GenerateUniqueName);

            await using (Stream? fileStream = await storageFile.OpenStreamForWriteAsync())
            {
                using MemoryStream memoryStream = new(imageBytes);
                memoryStream.Position = 0;
                await memoryStream.CopyToAsync(fileStream);
            }

            // 返回本地文件的路径
            return storageFile.Path;    
        }
        catch(Exception)
        {
            return null;
        }
    }
}