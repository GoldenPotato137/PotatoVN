using Windows.Storage;

namespace GalgameManager.Helpers;

public static class DownloadHelper
{
    /// <summary>
    /// 从网络下载图片并保存到本地
    /// </summary>
    /// <param name="imageUrl">图片链接</param>
    /// <param name="retry">这是第几次重试</param>
    /// <param name="fileNameWithoutExtension">目标文件名（不带扩展名）</param>
    /// <param name="onException">失败时回调，若为Http异常则等到重试次数满后触发，否则在有异常时立刻触发</param>
    /// <returns>本地文件路径, 如果下载失败则返回null</returns>
    public static async Task<string?> DownloadAndSaveImageAsync(string? imageUrl, int retry = 0, 
        string? fileNameWithoutExtension = null, Action<Exception>? onException = null)
    {
        try
        {
            if (imageUrl == null) return null;
            HttpClient httpClient = new();
            HttpResponseMessage response = await httpClient.GetAsync(imageUrl);
            response.EnsureSuccessStatusCode();

            var imageBytes = await response.Content.ReadAsByteArrayAsync();

            StorageFolder? localFolder = ApplicationData.Current.LocalFolder;
            localFolder = await localFolder.CreateFolderAsync("Images", CreationCollisionOption.OpenIfExists);
            var fileName = fileNameWithoutExtension is not null
                ? $"{fileNameWithoutExtension}{GetImageFormat(imageBytes)}"
                : imageUrl[(imageUrl.LastIndexOf('/') + 1)..];
            if (fileName == string.Empty) fileName = imageUrl;
            if (fileName.Contains('?')) fileName = fileName[..fileName.IndexOf('?')];
            StorageFile? storageFile;
            try
            {
                storageFile = await localFolder.CreateFileAsync(fileName, CreationCollisionOption.ReplaceExisting);
            }
            catch (FileNotFoundException)
            {
                fileName = fileNameWithoutExtension ?? Path.GetRandomFileName(); //随机文件名
                var format = GetImageFormat(imageBytes);
                if (format != string.Empty)
                    fileName = fileName[..fileName.LastIndexOf('.')] + format;
                storageFile = await localFolder.CreateFileAsync(fileName, CreationCollisionOption.ReplaceExisting);
            }

            await using (Stream? fileStream = await storageFile.OpenStreamForWriteAsync())
            {
                using MemoryStream memoryStream = new(imageBytes);
                memoryStream.Position = 0;
                await memoryStream.CopyToAsync(fileStream);
            }

            // 返回本地文件的路径
            return storageFile.Path;
        }
        catch (HttpRequestException e)
        {
            if (retry < 3)
            {
                await Task.Delay(5000);
                return await DownloadAndSaveImageAsync(imageUrl, retry + 1);
            }
            onException?.Invoke(e);
            return null;
        }
        catch(Exception e)
        {
            onException?.Invoke(e);
            return null;
        }
    }
    
    /// <summary>
    /// 试图识别图片格式
    /// </summary>
    /// <param name="bytes">图片</param>
    /// <returns>后缀名，若无法识别则返回空</returns>
    private static string GetImageFormat(byte[] bytes)
    {
        switch (bytes)
        {
            //jpg
            case [0xFF, 0xD8, ..]:
                return ".jpg";
            //png
            case [0x89, 0x50, 0x4E, 0x47, ..]:
                return ".png";
            //gif
            case [0x47, 0x49, 0x46, 0x38, ..]:
                return ".gif";
            //bmp
            case [0x42, 0x4D, ..]:
                return ".bmp";
            default:
                return string.Empty;
        }
    }
}