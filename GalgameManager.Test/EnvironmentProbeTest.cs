using GalgameManager.Helpers;

namespace GalgameManager.Test;

/// <summary>
/// 测试进程环境探针：确认各类运行时设施在未打包、无WinAppSDK bootstrap的NUnit进程中的行为。
/// 与具体service无关的通用探针统一放这里。
/// </summary>
[TestFixture]
public class EnvironmentProbeTest
{
    [Test]
    public void GetLocalized_UnpackagedTestProcess_DoesNotThrow()
    {
        // ResourceLoader静态初始化在无bootstrap的测试进程中不会炸，取不到的key会兜底返回key本身
        var result = string.Empty;
        Assert.DoesNotThrow(() => result = "DefinitivelyNotARealKey_12345".GetLocalized());
        Assert.That(result, Is.Not.Null);
    }
}
