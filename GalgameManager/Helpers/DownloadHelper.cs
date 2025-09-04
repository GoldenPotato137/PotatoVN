using System.Net;
using Windows.Storage;
using Windows.Storage.Pickers;
using GalgameManager.Contracts.Services;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

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
            httpClient.Timeout = TimeSpan.FromSeconds(10); // 10s内收不到响应则超时
            HttpResponseMessage response = await httpClient.GetAsync(imageUrl);
            response.EnsureSuccessStatusCode();

            var imageBytes = await response.Content.ReadAsByteArrayAsync();

            StorageFolder localFolder = await FileHelper.GetFolderAsync(FileHelper.FolderType.Images);
            var fileName = fileNameWithoutExtension is not null
                ? $"{fileNameWithoutExtension}{GetImageFormat(imageBytes)}"
                : imageUrl[(imageUrl.LastIndexOf('/') + 1)..];
            if (fileName == string.Empty) fileName = imageUrl;
            if (fileName.Contains('?')) fileName = fileName[..fileName.IndexOf('?')];
            if (fileName.Contains('%')) fileName = Uri.UnescapeDataString(fileName);
            fileName = fileName.RemoveInvalidChars();
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
        catch (Exception e)
        {
            if (e is (TaskCanceledException or TimeoutException or HttpRequestException) 
                and not HttpRequestException { StatusCode: HttpStatusCode.NotFound })
            {
                if (retry < 3)
                {
                    await Task.Delay(1000);
                    return await DownloadAndSaveImageAsync(imageUrl, retry + 1, fileNameWithoutExtension, onException);
                }
            }
            onException?.Invoke(e);
            return null;
        }
    }
    
    public static Task<string?> DownloadAndSaveImageWithDiffThread(string? imageUrl, int retry = 0, 
        string? fileNameWithoutExtension = null, Action<Exception>? onException = null)
    {
        return Task.Run(() => DownloadAndSaveImageAsync(imageUrl, retry, fileNameWithoutExtension, onException));
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

    /// <summary>
    /// 从本地文件读取图片
    /// </summary>
    /// <returns>图片路径</returns>
    public static async Task<string?> PickImageAsync()
    {
        FileOpenPicker openPicker = new()
        {
            ViewMode = PickerViewMode.Thumbnail,
            SuggestedStartLocation = PickerLocationId.PicturesLibrary
        };
        WinRT.Interop.InitializeWithWindow.Initialize(openPicker, App.MainWindow!.GetWindowHandle());
        openPicker.FileTypeFilter.Add(".jpg");
        openPicker.FileTypeFilter.Add(".jpeg");
        openPicker.FileTypeFilter.Add(".png");
        openPicker.FileTypeFilter.Add(".bmp");
        StorageFile? file = await openPicker.PickSingleFileAsync();
        if (file == null) return null;
        StorageFile newFile = await file.CopyAsync(await FileHelper.GetFolderAsync(FileHelper.FolderType.Images),
            $"{file.Name}", NameCollisionOption.ReplaceExisting);
        return newFile.Path;
    }

    public static void DeleteImgIfExists(string? path)
    {
        if (path == null) return;
        try
        {
            File.Delete(path);
        }
        catch (Exception e)
        {
            App.GetService<IInfoService>().DeveloperEvent(e: e);
        }
    }

    /// <summary>
    /// 处理图像，裁剪下部 1/3，并根据提供的函数应用透明度。
    /// </summary>
    /// <param name="inputPath">输入图像文件的路径。</param>
    /// <param name="outputPath">处理后图像的保存路径（应为png格式）</param>
    /// <param name="transparencyFunction">
    ///     一个函数，接收像素坐标 (x, y) 和裁剪后图像尺寸 (width, height)，
    ///     返回该像素的alpha (0.0 = 完全透明, 1.0 = 完全不透明)。
    /// </param>
    /// <param name="cutBottom">是否裁切底部30%（用来处理vndb游戏截图的对话框）</param>
    public static void ProcessImage(string inputPath, string outputPath, bool cutBottom,
        Func<int, int, int, int, float>? transparencyFunction = null)
    {
        using Image<Rgba32> image = Image.Load<Rgba32>(inputPath);
        var newHeight = cutBottom ? (int)Math.Round(image.Height * (2.0 / 3.0)) : image.Height;
        if (newHeight < 1) throw new ArgumentException("裁剪后的高度必须大于 0。");
        Rectangle cropRectangle = new Rectangle(0, 0, image.Width, newHeight);
        image.Mutate(ctx => ctx.Crop(cropRectangle));

        //应用基于坐标的透明度计算
        ApplyPixelTransparency();
        image.SaveAsPng(outputPath);
        return;

        void ApplyPixelTransparency()
        {
            int width = image.Width, height = image.Height;
            image.ProcessPixelRows(accessor =>
            {
                for (var y = 0; y < height; y++)
                {
                    Span<Rgba32> pixelRow = accessor.GetRowSpan(y);
                    for (var x = 0; x < width; x++)
                    {
                        ref Rgba32 pixel = ref pixelRow[x];
                        var originalAlpha = pixel.A;

                        var factor = transparencyFunction is null
                            ? CalcAlpha(x, y, width, height)
                            : Math.Clamp(transparencyFunction(x, y, width, height), 0f, 1f);
                        var newAlphaFloat = originalAlpha * factor;
                        pixel.A = (byte)Math.Clamp(MathF.Round(newAlphaFloat), 0, 255);
                    }
                }
            });
        }
    }
    
    private static float CalcAlpha(int col, int row, int width, int height)
    {
        if (width <= 1 || height <= 1) return 1f; // 避免除零
        float normX = (float)col / (width - 1), normY = (float)row / (height - 1);
        var globalAlpha = 0.35f;
            
        // --- Rule 1: 左侧渐变 (非线性) ---
        var alphaLeft = 1.0f;
        if (normX <= 0.7f)
        {
            var relativeXLeft = normX / 0.7f;
            var easeInPower = 3.0f; // 可以调整这个幂次来改变曲线陡峭程度
            alphaLeft = 0.2f + (1.0f - 0.2f) * (float)Math.Pow(relativeXLeft, easeInPower);
            alphaLeft = Math.Clamp(alphaLeft, 0.2f, 1.0f);
        }
        // --- Rule 2: 右侧渐变 (线性) ---
        var alphaRight = 1.0f;
        var rightThreshold = 0.6f;
        if (normX >= rightThreshold)
        {
            var relativeXRight = (normX - rightThreshold) / (1.0f - rightThreshold);
            // 线性渐变到 0.2
            alphaRight = 1.0f + (0.2f - 1.0f) * relativeXRight;
            alphaRight = Math.Clamp(alphaRight, 0.2f, 1.0f);
        }
        // --- Rule 3: 底部渐变 (线性) ---
        var alphaBottom = 1.0f;
        var bottomThreshold = 0.4f;
        if (normY >= bottomThreshold)
        {
            var relativeYBottom = (normY - bottomThreshold) / (1.0f - bottomThreshold);
            // 线性渐变到 0.0
            alphaBottom = globalAlpha - globalAlpha * relativeYBottom;
            alphaBottom = Math.Clamp(alphaBottom, 0.0f, 1.0f);
        }

        var finalAlpha = Math.Min(alphaLeft, Math.Min(alphaRight, alphaBottom));
        finalAlpha = Math.Min(finalAlpha, globalAlpha);
        return finalAlpha;
    }
}
