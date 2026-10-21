using Windows.Graphics.Imaging;
using Windows.Storage.Streams;
using GalgameManager.Helpers;

namespace GalgameManager.Test.Helpers;

[TestFixture]
public class DownloadHelperTest
{
    private string _tempDir = null!;

    [SetUp]
    public void SetUp()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"PotatoVN_DownloadHelperTest_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
    }

    [TearDown]
    public void TearDown()
    {
        if (Directory.Exists(_tempDir)) Directory.Delete(_tempDir, true);
    }

    [Test]
    public async Task ProcessImageAsync_CutBottom_KeepsTopTwoThirdsAndAppliesAlpha()
    {
        var input = Path.Combine(_tempDir, "input.png");
        var output = Path.Combine(_tempDir, "output.png");
        //上半部分红色、下半部分蓝色
        await WriteImageAsync(input, BitmapEncoder.PngEncoderId, 30, 30, y => y < 15 ? (0, 0, 255) : (255, 0, 0));

        await DownloadHelper.ProcessImageAsync(input, output, true, (_, _, _, _) => 0.5f);

        (byte[] pixels, int width, int height) = await ReadImageAsync(output);
        Assert.Multiple(() =>
        {
            Assert.That(width, Is.EqualTo(30));
            Assert.That(height, Is.EqualTo(20));
            Assert.That(pixels[..4], Is.EqualTo(new byte[] { 0, 0, 255, 128 }));
            Assert.That(pixels[^4..], Is.EqualTo(new byte[] { 255, 0, 0, 128 }));
        });
    }

    [Test]
    public async Task ProcessImageAsync_JpegWithDefaultAlpha_KeepsSizeAndLimitsAlpha()
    {
        var input = Path.Combine(_tempDir, "input.jpg");
        var output = Path.Combine(_tempDir, "output.png");
        await WriteImageAsync(input, BitmapEncoder.JpegEncoderId, 40, 24, _ => (128, 128, 128));

        await DownloadHelper.ProcessImageAsync(input, output, false);

        (byte[] pixels, int width, int height) = await ReadImageAsync(output);
        var alphas = pixels.Where((_, i) => i % 4 == 3).ToArray();
        Assert.Multiple(() =>
        {
            Assert.That(width, Is.EqualTo(40));
            Assert.That(height, Is.EqualTo(24));
            Assert.That(alphas.Max(), Is.LessThanOrEqualTo((byte)90)); //全局alpha上限为0.35
            Assert.That(alphas[^1], Is.EqualTo(0)); //底部渐变到完全透明
        });
    }

    [Test]
    public void ProcessImageAsync_NotAnImage_Throws()
    {
        var input = Path.Combine(_tempDir, "input.png");
        File.WriteAllText(input, "not an image");

        Assert.That(async () => await DownloadHelper.ProcessImageAsync(input, Path.Combine(_tempDir, "output.png"),
            false), Throws.Exception);
    }

    private static async Task WriteImageAsync(string path, Guid encoderId, int width, int height,
        Func<int, (int B, int G, int R)> colorOfRow)
    {
        var pixels = new byte[width * height * 4];
        for (var y = 0; y < height; y++)
        {
            (int b, int g, int r) = colorOfRow(y);
            for (var x = 0; x < width; x++)
            {
                var index = (y * width + x) * 4;
                pixels[index] = (byte)b;
                pixels[index + 1] = (byte)g;
                pixels[index + 2] = (byte)r;
                pixels[index + 3] = 255;
            }
        }

        using IRandomAccessStream stream = File.Create(path).AsRandomAccessStream();
        BitmapEncoder encoder = await BitmapEncoder.CreateAsync(encoderId, stream);
        encoder.SetPixelData(BitmapPixelFormat.Bgra8, BitmapAlphaMode.Straight, (uint)width, (uint)height, 96, 96,
            pixels);
        await encoder.FlushAsync();
    }

    private static async Task<(byte[] Pixels, int Width, int Height)> ReadImageAsync(string path)
    {
        using IRandomAccessStream stream = File.OpenRead(path).AsRandomAccessStream();
        BitmapDecoder decoder = await BitmapDecoder.CreateAsync(stream);
        PixelDataProvider pixelData = await decoder.GetPixelDataAsync(BitmapPixelFormat.Bgra8,
            BitmapAlphaMode.Straight, new BitmapTransform(), ExifOrientationMode.IgnoreExifOrientation,
            ColorManagementMode.DoNotColorManage);
        return (pixelData.DetachPixelData(), (int)decoder.PixelWidth, (int)decoder.PixelHeight);
    }
}
