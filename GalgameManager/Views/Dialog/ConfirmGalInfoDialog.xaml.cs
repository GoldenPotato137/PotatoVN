#pragma warning disable MVVMTK0049 //警告INotifyPropertyChanged无法NativeAOT，等后续处理
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GalgameManager.Contracts.Services;
using GalgameManager.Enums;
using GalgameManager.Helpers;
using GalgameManager.Models;
using Microsoft.UI.Xaml;

namespace GalgameManager.Views.Dialog;

[INotifyPropertyChanged]
public partial class ConfirmGalInfoDialog
{
    public List<RssType> RssTypes { get; }= new() { RssType.Bangumi, RssType.Vndb, RssType.Ymgal, RssType.Cngal, RssType.Hikarinagi };
    [ObservableProperty] private Galgame _galgame = null!;
    [ObservableProperty] private string? _id = string.Empty;
    [ObservableProperty] private RssType _selectedRssType = RssType.Bangumi;
    [ObservableProperty] private string _hint = null!;
    [ObservableProperty] private Visibility _isPhrasing = Visibility.Collapsed;
    private readonly IGalgameCollectionService _service;
    
    // 添加跟踪原始值的属性
    private string _originalName = string.Empty;
    private Dictionary<int, string?> _originalIds = new();
    private RssType _originalSelectedRssType = RssType.None;

    public ConfirmGalInfoDialog(Galgame targetGame, Galgame? fetchedMeta, IGalgameCollectionService service)
    {
        InitializeComponent();
        RequestedTheme = App.MainWindow?.Content is Microsoft.UI.Xaml.FrameworkElement element ? element.RequestedTheme : RequestedTheme;
        XamlRoot = App.MainWindow!.Content.XamlRoot;
        
        Galgame = fetchedMeta ?? new Galgame(targetGame.Name.Value ?? string.Empty);
        _service = service;
        
        // 保存原始名称和ID
        _originalName = Galgame.Name.Value ?? string.Empty;
        foreach (RssType rssType in RssTypes)
        {
            _originalIds[(int)rssType] = Galgame.Ids[(int)rssType];
        }
        // 保存原始选中的RssType
        _originalSelectedRssType = SelectedRssType;
        
        Update();
        PrimaryButtonText = "Yes".GetLocalized();
        SecondaryButtonText = "Cancel".GetLocalized();
    }

    private void Update()
    {
        Id = Galgame.Ids[(int)SelectedRssType];
        Title = Galgame.Description.Value?.Length > 0
            ? "ConfirmGalInfoDialog_Title_Correct".GetLocalized()
            : "ConfirmGalInfoDialog_Title_NotFound".GetLocalized();
        List<RssType> currentId = RssTypes.Where(rss => !string.IsNullOrWhiteSpace(Galgame.Ids[(int)rss])).ToList();
        Hint = "ConfirmGalInfoDialog_Hint".GetLocalized() + "\n" +
               (currentId.Count == 0
                   ? "ConfirmGalInfoDialog_NoID".GetLocalized()
                   : "ConfirmGalInfoDialog_ID".GetLocalized(string.Join(',', currentId)));
    }

    [RelayCommand]
    private async Task FetchInfo()
    {
        IsPhrasing = Visibility.Visible;
        IsPrimaryButtonEnabled = IsSecondaryButtonEnabled = false;
        
        var nameChanged = _originalName != (Galgame.Name.Value ?? string.Empty);
        var idChanged = false;
        var rssTypeChanged = _originalSelectedRssType != SelectedRssType;
        RssType targetRssType = SelectedRssType;
        
        // 检查ID是否有变化
        foreach (RssType rssType in RssTypes)
        {
            var originalId = _originalIds.ContainsKey((int)rssType) ? _originalIds[(int)rssType] : null;
            if (originalId != Galgame.Ids[(int)rssType])
            {
                idChanged = true;
                break;  // 一旦发现有变化，立即退出循环
            }
        }
        
        // 根据是否修改了ID决定搜索逻辑
        var shouldClearIds = !idChanged && (nameChanged || rssTypeChanged);
        
        if (idChanged)
        {
            // 如果有ID修改，找到第一个非空ID作为搜索源
            foreach (RssType rssType in RssTypes)
            {
                if (!string.IsNullOrWhiteSpace(Galgame.Ids[(int)rssType]))
                {
                    targetRssType = rssType;
                    break;
                }
            }
        }

        // 如果id都是空的，使用RssType.None搜索
        if (RssTypes.All(rssType => string.IsNullOrWhiteSpace(Galgame.Ids[(int)rssType])))
        {
            targetRssType = RssType.None;
        }

        if (shouldClearIds)
        {
            // 删除所有ID，以强制从名字搜索
            foreach (RssType rssType in RssTypes)
            {
                Galgame.Ids[(int)rssType] = null;
            }
        }

        await _service.ParseGalInfoOnlyAsync(Galgame, targetRssType);
        IsPhrasing = Visibility.Collapsed;
        IsPrimaryButtonEnabled = IsSecondaryButtonEnabled = true;
        Update();
        
        // 更新原始值
        _originalName = Galgame.Name.Value ?? string.Empty;
        foreach (RssType rssType in RssTypes)
        {
            _originalIds[(int)rssType] = Galgame.Ids[(int)rssType];
        }
        _originalSelectedRssType = SelectedRssType;
    }

    partial void OnIdChanged(string? value)
    {
        Galgame.Ids[(int)SelectedRssType] = string.IsNullOrWhiteSpace(value) ? null : value;
        Galgame.UpdateMixedId();
        Update();
    }

    partial void OnSelectedRssTypeChanged(RssType value) => Id = Galgame.Ids[(int)value];
}

#pragma warning restore MVVMTK0049