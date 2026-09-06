using GalgameManager.Models;

namespace GalgameManager.Test.Models;

[TestFixture]
public class GalgameTest
{
    // 验证初始构造的LockableProperty实例（未经过整体替换）修改.Value也会触发GalPropertyChanged
    [Test]
    public void InitialLockableProperty_ValueEdit_RaisesGalPropertyChanged()
    {
        Galgame game = new("测试游戏");
        List<string> changed = new();
        game.GalPropertyChanged += (_, name, _) => changed.Add(name);

        game.Developer.Value = "新开发商";
        game.Engine.Value = "新引擎";

        Assert.That(changed, Is.EqualTo(new[] { nameof(Galgame.Developer), nameof(Galgame.Engine) }));
    }

    // 验证整体替换LockableProperty属性后的绑定行为：替换本身不触发GalPropertyChanged
    // （批量更新场景由GalgameMutated等显式路径兜底），旧实例被解绑、新实例已绑定
    [Test]
    public void ReplacedLockableProperty_OldInstanceUnbound_NewInstanceBound()
    {
        Galgame game = new("测试游戏");
        LockableProperty<string> oldDeveloper = game.Developer;
        var count = 0;
        game.GalPropertyChanged += (_, _, _) => count++;

        game.Developer = "整体替换";
        Assert.That(count, Is.EqualTo(0));

        oldDeveloper.Value = "修改旧实例";
        Assert.That(count, Is.EqualTo(0));

        game.Developer.Value = "修改新实例";
        Assert.That(count, Is.EqualTo(1));
    }
}
