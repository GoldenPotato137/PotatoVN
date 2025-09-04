using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using GalgameManager.Contracts.Services;
using GalgameManager.Enums;
using GalgameManager.Helpers;
using GalgameManager.Models.Sources;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
namespace GalgameManager.Views.Dialog;

// treeview有bug，不能使用它默认的多选（会莫名奇妙地越界），因此我们自己实现选择功能
public sealed partial class SelectToScanFolderDialog
{
    public bool Canceled = true;
    public List<string> SelectedPaths { get; private set; } = [];
    private readonly List<ExplorerItem> _allItems = [];
    private readonly GalgameSourceBase _source;
    
    public SelectToScanFolderDialog(GalgameSourceBase source)
    {
        InitializeComponent();
        PrimaryButtonText = "Yes".GetLocalized();
        SecondaryButtonText = "Cancel".GetLocalized();
        RequestedTheme = App.MainWindow?.Content is FrameworkElement element ? element.RequestedTheme : RequestedTheme;
        XamlRoot = App.MainWindow!.Content!.XamlRoot;
        DefaultButton = ContentDialogButton.Primary;
        Opened += OnOpened;
        PrimaryButtonClick += OnPrimaryButtonClick;
        
        _source = source;
    }

    private async void OnOpened(ContentDialog sender, ContentDialogOpenedEventArgs args)
    {
        List<string> paths = [];
        List<ExplorerItem> roots = []; //根目录下的文件夹及游戏
        HashSet<Uri> unSelectedPath = [];
        try
        {
            FetchingFoldersPanel.Visibility = Visibility.Visible;
            FolderTreeView.Visibility = Visibility.Collapsed;
            await foreach ((string? path, string msg) p in _source.ScanAllGalgames())
                if (p.path is not null)
                    paths.Add(p.path);
            // 把子库的路径也加进来，方便玩家统一管理
            foreach (GalgameSourceBase subSource in _source.SubSources)
                await foreach ((string? path, string msg) p in subSource.ScanAllGalgames())
                    if (p.path is not null)
                        paths.Add(p.path);
            await Task.Run(RebuildTreeFromPath);
            FolderTreeView.ItemsSource = new ObservableCollection<ExplorerItem>(roots);
            foreach (ExplorerItem item in roots) GetAllItem(item);
            
            foreach (var path in _source.DontScanPath) unSelectedPath.Add(new Uri(path));
            foreach (ExplorerItem item in _allItems.Where(it => !string.IsNullOrEmpty(it.Path)))
                if (!unSelectedPath.Contains(new Uri(item.Path)))
                    item.IsSelected = true;
            foreach (ExplorerItem item in roots.Where(it => it.IsRoot)) item.Init();
            
            FetchingFoldersPanel.Visibility = Visibility.Collapsed;
            FolderTreeView.Visibility = Visibility.Visible;
        }
        catch (Exception e)
        {
            App.GetService<IInfoService>()
                .Event(EventType.PageError, InfoBarSeverity.Error, title: "Oops!", msg: e.ToString());
        }

        return;

        void RebuildTreeFromPath()
        {
            Dictionary<ExUri, ExplorerItem> map = [];
            List<ExUri> folders = [];
            foreach (var path in paths)
            {
                ExUri uri = new(path.EndsWith('/') ? path : $"{path}/"), parent = uri.Parent;
                ExplorerItem current = new() { Name = uri.Name, Type = ExplorerItem.ExplorerItemType.Game, Path = path};
                map[uri] = current;
                if (!map.ContainsKey(parent))
                {
                    ExplorerItem pItem = new() { Name = parent.Name, Type = ExplorerItem.ExplorerItemType.Folder };
                    map[parent] = pItem;
                    folders.Add(parent);
                }
                current.Parent = map[parent];
            }
            // 把所有folder的LCA（以及LCA的LCA们）都添加进列表中，以下算法效率非常低，如果folder数目超级多很有可能会慢，可以考虑更高效的算法
            for (var i = 0; i < folders.Count; i++)
                for (var j = i + 1; j < folders.Count; j++)
                {
                    ExUri lca = folders[i].Lca(folders[j]);
                    if (!map.TryGetValue(lca, out ExplorerItem? lcaNode))
                    {
                        lcaNode = new ExplorerItem { Name = lca.Name, Type = ExplorerItem.ExplorerItemType.Folder };
                        map[lca] = lcaNode;
                        folders.Add(lca);
                    }
                }
            foreach (ExUri folder in folders)
            {
                ExUri current = folder;
                while (true)
                {
                    ExUri parent = current.Parent;
                    if (parent == current) break; //根目录
                    if (map.TryGetValue(parent, out ExplorerItem? pItem))
                    {
                        map[folder].Parent = pItem;
                        break;
                    }
                    current = parent;
                }
            }
            // 构造Children数组
            foreach (ExplorerItem item in map.Values) item.Parent?.Children.Add(item);
            // 只添加根节点
            foreach (ExplorerItem item in map.Values)
                if (item is { IsRoot: true })
                    roots.Add(item);
        }

        void GetAllItem(ExplorerItem current)
        {
            _allItems.Add(current);
            foreach (ExplorerItem child in current.Children)
                GetAllItem(child);
        }
    }
    
    private async void OnPrimaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
    {
        try
        {
            Canceled = false;
            await Task.Run(() =>
            {
                _source.DontScanPath.Clear();
                foreach (ExplorerItem item in _allItems)
                    if (!string.IsNullOrEmpty(item.Path) && !item.IsSelected)
                        _source.DontScanPath.Add(item.Path);
                _source.UpdateDontScanPath();
                foreach (GalgameSourceBase src in _source.GetSubAncestorsSources())
                    App.GetService<IGalgameSourceCollectionService>().Save(src);
                
                foreach (ExplorerItem item in _allItems)
                    if (!string.IsNullOrEmpty(item.Path) && item.IsSelected)
                        SelectedPaths.Add(item.Path);
            });
        }
        catch (Exception e)
        {
            App.GetService<IInfoService>().DeveloperEvent(e: e);
        }
    }
}

public partial class ExplorerItem : ObservableObject
{
    private static int _calculateSelecting = 0; //防止处理selected关系时与玩家操作混杂
    public enum ExplorerItemType
    {
        Folder,
        Game,
    }

    public string Name { get; set; } = string.Empty;
    public string Path { get; set; } = string.Empty;
    public ExplorerItemType Type { get; set; }
    public ObservableCollection<ExplorerItem> Children { get; set; } = [];
    public ExplorerItem? Parent { get; set; }
    public bool IsRoot => Parent is null || Parent == this;
    [ObservableProperty] private bool _isSelected;
    
    private int _childSelectedCount; //所有叶子节点（游戏）的已选择数
    private int _childCount; //所有叶子节点（游戏）的总数

    public void Init()
    {
        _calculateSelecting++;
        _childCount = 0;
        foreach (ExplorerItem child in Children)
        {
            child.Init();
            _childCount += child._childCount;
        }
        if (Children.Count == 0) _childCount = 1;
        UpdateIsSelected();
        _calculateSelecting--;
    }

    partial void OnIsSelectedChanged(bool value)
    {
        if (_calculateSelecting > 0) return; // 这个函数用来处理用户操作引起的变化
        _calculateSelecting++;
        // 更新子节点
        SetSelected(this, value);
        // 往上更新
        ExplorerItem? current = this;
        do
        {
            current.UpdateIsSelected();
            current = current.Parent;
        } while (current is not null);
        _calculateSelecting--;
        return;

        void SetSelected(ExplorerItem now, bool isSelected)
        {
            now.IsSelected = isSelected;
            foreach (ExplorerItem child in now.Children)
                SetSelected(child, isSelected);
            now.UpdateIsSelected();
        }
    }

    private void UpdateIsSelected()
    {
        _calculateSelecting++;
        if (Children.Count == 0) // 叶子节点
            _childSelectedCount = IsSelected ? 1 : 0;
        else // 文件夹节点
        {
            _childSelectedCount = 0;
            foreach (ExplorerItem child in Children)
                _childSelectedCount += child._childSelectedCount;
            IsSelected = _childCount == _childSelectedCount; // 暂时不处理半选状态
        }
        _calculateSelecting--;
    }
}

public class ExplorerItemTemplateSelector : DataTemplateSelector
{
    public DataTemplate FolderTemplate { get; set; } = null!;
    public DataTemplate GameTemplate { get; set; } = null!;

    protected override DataTemplate SelectTemplateCore(object item)
    {
        var explorerItem = (ExplorerItem)item;
        return explorerItem.Type == ExplorerItem.ExplorerItemType.Folder
            ? FolderTemplate
            : GameTemplate;
    }
}