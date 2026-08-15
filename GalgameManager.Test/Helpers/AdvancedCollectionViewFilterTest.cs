using System.Collections.ObjectModel;
using CommunityToolkit.WinUI.Collections;

namespace GalgameManager.Test.Helpers;

/// <summary>
/// 固化 <see cref="AdvancedCollectionView"/> 的过滤刷新契约。<br/>
/// 这里测的是第三方集合的行为，但插件商店（<c>PluginStoreViewModel</c>）的过滤功能正确与否完全依赖它：
/// 曾经踩过的坑是"条件变化时重新给Filter赋值"，看起来天经地义，实际上一次都不会刷新。
/// </summary>
[TestFixture]
public class AdvancedCollectionViewFilterTest
{
    private ObservableCollection<string> _source = null!;
    private AdvancedCollectionView _view = null!;
    private bool _onlyLong;

    [SetUp]
    public void SetUp()
    {
        _source = [];
        _view = new AdvancedCollectionView(_source);
        foreach (var s in new[] { "a", "bbbb", "c", "dddd" })
            _source.Add(s);
        _onlyLong = false;
    }

    /// 只捕获this的lambda：每次生成的委托Method与Target都相同，Delegate的==因此为true
    private Predicate<object> CreateFilter() => o => !_onlyLong || ((string)o).Length > 1;

    [Test]
    public void ReassigningFilter_ThatOnlyCapturesThis_DoesNotRefresh()
    {
        _view.Filter = CreateFilter();
        Assert.That(_view, Has.Count.EqualTo(4));

        _onlyLong = true;
        _view.Filter = CreateFilter();

        Assert.That(_view, Has.Count.EqualTo(4),
            "Filter的setter有 if (_filter == value) return; 短路，重新赋值等价委托不会刷新");
    }

    [Test]
    public void RefreshFilter_AfterConditionChanged_Refreshes()
    {
        _view.Filter = CreateFilter();
        Assert.That(_view, Has.Count.EqualTo(4));

        _onlyLong = true;
        _view.RefreshFilter();

        Assert.That(_view, Has.Count.EqualTo(2));
    }

    [Test]
    public void EquivalentThisCapturingDelegates_CompareEqual()
    {
        Predicate<object> first = CreateFilter();
        Predicate<object> second = CreateFilter();

        Assert.Multiple(() =>
        {
            Assert.That(ReferenceEquals(first, second), Is.False, "确实是两个不同的委托实例");
            Assert.That(first == second, Is.True, "但Delegate的==比较的是Method+Target，所以相等");
        });
    }
}
