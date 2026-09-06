using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using GalgameManager.Contracts.Services;
using GalgameManager.Enums;
using GalgameManager.Helpers;
using GalgameManager.Models;
using GalgameManager.Models.Filters;
using GalgameManager.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using GalgameManager.WinApp.Base.Contracts.NavigationApi.NavigateParameters;

namespace GalgameManager.Views.GalgamePagePanel;

public partial class GameHeaderPanel
{
    private readonly ICategoryService _categoryService = App.GetService<ICategoryService>();
    private readonly IInfoService _infoService = App.GetService<IInfoService>();
    private readonly IFilterService _filterService = App.GetService<IFilterService>();
    private readonly INavigationService _navigationService = App.GetService<INavigationService>();
    private readonly ILocalSettingsService _localSettingsService = App.GetService<ILocalSettingsService>();
    private readonly IStaffService _staffService = App.GetService<IStaffService>();
    private Galgame? _lastGame;
    private readonly ObservableCollection<GameHeaderPanelStaffList> _staffListSource = new();
    private readonly ObservableCollection<GameHeaderPanelDeveloperList> _developerListSource = new();
    
    // 标题属性
    public string PrimaryTitleText => GetTitleText(PrimaryTitleType);
    public string SecondaryTitleText => GetTitleText(SecondaryTitleType);
    public Visibility IsSecondaryTitleVisible => string.IsNullOrEmpty(SecondaryTitleText) ? Visibility.Collapsed : Visibility.Visible;
    private DisplayName PrimaryTitleType { get; set; }
    private DisplayName SecondaryTitleType { get; set; }

    public GameHeaderPanel()
    {
        InitializeComponent();
        StaffList.ItemsSource = _staffListSource;
        DeveloperList.ItemsSource = _developerListSource;
        Unloaded += (_, _) =>
        {
            _staffService.OnGameStaffChanged -= StaffServiceOnOnGameStaffChanged;
            _localSettingsService.OnSettingChanged -= LocalSettingsServiceOnOnSettingChanged;
            if (Game is not null)
                Game.HeaderImagePath.OnValueChanged -= HeaderImagePathOnOnValueChanged;
        };
        Loaded += (_, _) =>
        {
            _staffService.OnGameStaffChanged += StaffServiceOnOnGameStaffChanged;
            _localSettingsService.OnSettingChanged += LocalSettingsServiceOnOnSettingChanged;
        };
        return;

        void LocalSettingsServiceOnOnSettingChanged(string key, object? value)
        {
            switch (key)
            {
                case KeyValues.GalgamePagePrimaryTitleType:
                case KeyValues.GalgamePageSecondaryTitleType:
                    UiThreadInvokeHelper.Invoke(UpdateTitles);
                    break;
                case KeyValues.GalgamePageNewLayout_ShowPainter:
                case KeyValues.GalgamePageNewLayout_ShowSeiyu:
                case KeyValues.GalgamePageNewLayout_ShowWriter:
                case KeyValues.GalgamePageNewLayout_ShowMusician:
                    UiThreadInvokeHelper.Invoke(UpdateStaffs);
                    break;
                case KeyValues.GalgamePageNewLayout_CoverImage:
                case KeyValues.GalgamePageNewLayout_ShowHeaderImage:
                case KeyValues.GalgamePageNewLayout_ShowCoverWhenNoBackground:
                    UiThreadInvokeHelper.Invoke(UpdateHeaderImgAndCoverImg);
                    break;
                case KeyValues.GalgamePageNewLayout_ShowRating:
                    UiThreadInvokeHelper.Invoke(UpdateRatingVisibility);
                    break;
                case KeyValues.GalgamePageNewLayout_ShowExpectedPlayTime:
                    UiThreadInvokeHelper.Invoke(UpdatePlayTimeVisibility);
                    break;
            }
        }
        
        void StaffServiceOnOnGameStaffChanged(Galgame obj)
        {
            UiThreadInvokeHelper.Invoke(async () =>
            {
                if (obj != Game) return;
                await UpdateStaffs(); //加载新的Staff数据
            });
        }
    }

    protected async override void Update()
    {
        try
        {
            if (_lastGame is not null) _lastGame.HeaderImagePath.OnValueChanged -= HeaderImagePathOnOnValueChanged;
            if (Game is null) return;
            _lastGame = Game;
            Game.HeaderImagePath.OnValueChanged += HeaderImagePathOnOnValueChanged;
            
            // 加载标题类型设置
            await UpdateTitles();
            
            // 更新UI元素可见性
            await UpdateRatingVisibility();
            await UpdatePlayTimeVisibility();
            
            // 继续执行现有更新逻辑
            await UpdateHeaderImgAndCoverImg();
            await UpdateStaffs();
            UpdateDevelopers();
        }
        catch (Exception e)
        {
            _infoService.DeveloperEvent(e: e);
        }
    }
    
    private string GetTitleText(DisplayName titleType)
    {
        if (Game == null)
            return string.Empty;
            
        return titleType switch
        {
            DisplayName.ChineseName => Game.ChineseName.Value ?? string.Empty,
            DisplayName.OriginalName => Game.OriginalName.Value ?? string.Empty,
            DisplayName.Name => Game.Name.Value ?? string.Empty,
            DisplayName.None => string.Empty, 
            _ => string.Empty 
        };
    }

    
    // 更新标题显示，使用枚举值
    private async Task UpdateTitles()
    {
        if (Game == null) return;
        
        PrimaryTitleType = await _localSettingsService.ReadSettingAsync<DisplayName>(KeyValues.GalgamePagePrimaryTitleType);
        SecondaryTitleType = await _localSettingsService.ReadSettingAsync<DisplayName>(KeyValues.GalgamePageSecondaryTitleType);
        
        string primaryText = GetTitleText(PrimaryTitleType);
        
        if (string.IsNullOrEmpty(primaryText))
        {
            // 如果主标题为空，尝试使用副标题作为主标题，隐藏副标题
            string secondaryText = GetTitleText(SecondaryTitleType);
            
            if (!string.IsNullOrEmpty(secondaryText))
            {
                PrimaryTitleType = SecondaryTitleType;
                this.Bindings.Update();
                return;
            }
            
            // 主标题和副标题都为空，尝试使用剩下的一个选项
            foreach (DisplayName titleType in Enum.GetValues(typeof(DisplayName)))
            {
                if (titleType != PrimaryTitleType && titleType != SecondaryTitleType && titleType != DisplayName.None)
                {
                    string fallbackText = GetTitleText(titleType);
                    if (!string.IsNullOrEmpty(fallbackText))
                    {
                        PrimaryTitleType = titleType;
                        this.Bindings.Update();
                        return;
                    }
                }
            }
        }
        else
        {
            this.Bindings.Update();
        }
    }
    
    // 设置评分控件的可见性
    private async Task UpdateRatingVisibility()
    {
        if (Game is null) return;
        bool showRatingSetting = await _localSettingsService.ReadSettingAsync<bool>(KeyValues.GalgamePageNewLayout_ShowRating);
        bool shouldShow = showRatingSetting && Game.Rating.Value > 0;
        RatingGrid.Visibility = shouldShow ? Visibility.Visible : Visibility.Collapsed;
    }
    
    // 设置游戏时长控件的可见性
    private async Task UpdatePlayTimeVisibility()
    {
        if (Game is null) return;
        bool showPlayTimeSetting = await _localSettingsService.ReadSettingAsync<bool>(KeyValues.GalgamePageNewLayout_ShowExpectedPlayTime);
        bool shouldShow = showPlayTimeSetting && !string.IsNullOrEmpty(Game.ExpectedPlayTime.Value);
        // 直接设置UI元素可见性
        PlayTimePanel.Visibility = shouldShow ? Visibility.Visible : Visibility.Collapsed;
    }

    private async void HeaderImagePathOnOnValueChanged(string? arg)
    {
        try
        {
            await UpdateHeaderImgAndCoverImg();
        }
        catch (Exception e)
        {
            _infoService.DeveloperEvent(e: e);
        }
    }

    private void ClickDeveloper(object sender, RoutedEventArgs e)
    {
        if (Game is null) return;
        Category? category = _categoryService.GetDeveloperCategory(Game);
        if (category is null)
        {
            _infoService.Info(InfoBarSeverity.Error, msg: "HomePage_NoDeveloperCategory".GetLocalized());
            return;
        }
        _filterService.ClearFilters();
        _filterService.AddFilter(new CategoryFilter(category));
        _navigationService.NavigateTo(typeof(HomeViewModel).FullName!);
    }

    private void ClickEngine(object sender, RoutedEventArgs e)
    {
        if (Game is null) return;
        Category? category = _categoryService.GetEngineCategory(Game);
        if (category is null)
        {
            _infoService.Info(InfoBarSeverity.Error, msg: "HomePage_NoEngineCategory".GetLocalized());
            return;
        }
        _filterService.ClearFilters();
        _filterService.AddFilter(new CategoryFilter(category));
        _navigationService.NavigateTo(typeof(HomeViewModel).FullName!);
    }

    private void ClickStaff(object sender, RoutedEventArgs e)
    {
        if (sender is not HyperlinkButton button || button.DataContext is not Staff staff) return;
        NavigationHelper.NavigateToStaffPage(_navigationService,
            new StaffPageNavParameter { Staff = staff });
    }

    private void TitleSizeChanged(object sender, SizeChangedEventArgs e)
    {
        var width = Game?.Name.Value?.Length * 40 ?? 0;
        width = Math.Max(Game?.ChineseName.Value?.Length * 40 ?? 0, width);
        width = Math.Max(Game?.OriginalName.Value?.Length * 40 ?? 0, width);
        PrimaryTitleTextBlock.MaxWidth = Math.Max(Math.Min(e.NewSize.Width - 80, width), 50);
        SecondaryTitleTextBlock.MaxWidth = PrimaryTitleTextBlock.MaxWidth;
    }
    
    private async Task UpdateStaffs()
    {
        if (Game is null) return;
        List<(Career career, string settingKey)> careerSettings =
        [
            (Career.Painter, KeyValues.GalgamePageNewLayout_ShowPainter),
            (Career.Seiyu, KeyValues.GalgamePageNewLayout_ShowSeiyu),
            (Career.Writer, KeyValues.GalgamePageNewLayout_ShowWriter),
            (Career.Musician, KeyValues.GalgamePageNewLayout_ShowMusician)
        ];
        _staffListSource.Clear();
        foreach (var (career, settingKey) in careerSettings)
        {
            if (!await _localSettingsService.ReadSettingAsync<bool>(settingKey)) continue;
            
            List<Staff> tmp = _staffService.GetStaffs(Game).Where(s => (s.GetRelation(Game) ?? []).Contains(career))
                .ToList();
            if (tmp.Count == 0) continue;
            _staffListSource.Add(new GameHeaderPanelStaffList(career, tmp));
        }
    }

    private void UpdateDevelopers()
    {
        if (Game is null || string.IsNullOrEmpty(Game.Developer.Value))
        {
            _developerListSource.Clear();
            return;
        }

        _developerListSource.Clear();
        var developers = Game.Developer.Value.Split(new[] { "," }, StringSplitOptions.RemoveEmptyEntries)
            .Select(s => s.Trim())
            .Where(s => !string.IsNullOrEmpty(s));

        foreach (var dev in developers)
        {
            _developerListSource.Add(new GameHeaderPanelDeveloperList { Name = dev });
        }
    }

    private async Task UpdateHeaderImgAndCoverImg()
    {
        if (Game is null) return;

        var showBackground = await _localSettingsService.ReadSettingAsync<bool>(KeyValues.GalgamePageNewLayout_ShowHeaderImage);
        if (showBackground && File.Exists(Game.HeaderImagePath.Value))
        {
            ContentRoot.Margin = new Thickness(0, 20, 0, 0); //有Header图要给整个控件加点边距让它看起来好看点
            Cover.Visibility = (!await _localSettingsService.ReadSettingAsync<bool>(KeyValues
                .GalgamePageNewLayout_ShowCoverWhenNoBackground)).ToVisibility();
        }
        else
            Cover.Visibility = Visibility.Visible;
        
        if (await _localSettingsService.ReadSettingAsync<bool>(KeyValues.GalgamePageNewLayout_CoverImage) is false)
            Cover.Visibility = Visibility.Collapsed;
    }
}

public class GameHeaderPanelStaffList (Career career, List<Staff> staffsList)
{
    public string Career { get; set; } = $"Career_Relation_{career}".GetLocalized();
    public List<Staff> StaffsList { get; set; } = staffsList;
}

public class GameHeaderPanelDeveloperList : INotifyPropertyChanged
{
    public string Name { get; set; } = string.Empty;

    public event PropertyChangedEventHandler? PropertyChanged;
    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
