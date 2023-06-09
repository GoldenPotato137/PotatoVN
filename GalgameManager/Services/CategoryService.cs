﻿using System.Collections.Concurrent;
using System.Collections.ObjectModel;
using CommunityToolkit.WinUI;
using GalgameManager.Contracts.Services;
using GalgameManager.Core.Contracts.Services;
using GalgameManager.Enums;
using GalgameManager.Helpers;
using GalgameManager.Helpers.Phrase;
using GalgameManager.Models;
using Microsoft.UI.Dispatching;

namespace GalgameManager.Services;

public class CategoryService : ICategoryService
{
    private ObservableCollection<CategoryGroup> _categoryGroups = new();
    private readonly GalgameCollectionService _galgameService;
    private CategoryGroup? _developerGroup, _statusGroup;
    private bool _isInit;
    private readonly ILocalSettingsService _localSettings;
    private readonly BlockingCollection<Category> _queue = new();
    private readonly BgmPhraser _bgmPhraser;
    private readonly DispatcherQueue? _dispatcher;

    public CategoryService(ILocalSettingsService localSettings, IDataCollectionService<Galgame> galgameService)
    {
        _localSettings = localSettings;
        _galgameService = (galgameService as GalgameCollectionService)!;
        _galgameService.PhrasedEvent2 += UpdateCategory;
        _bgmPhraser = (BgmPhraser)_galgameService.PhraserList[(int)RssType.Bangumi];
        App.MainWindow.AppWindow.Closing += async (_, _) => await SaveAsync();
        _dispatcher = DispatcherQueue.GetForCurrentThread();
        Thread worker = new(Worker)
        {
            IsBackground = true
        };
        worker.Start();
    }

    public async Task Init()
    {
        if (_isInit) return;
        _categoryGroups = await _localSettings.ReadSettingAsync<ObservableCollection<CategoryGroup>>
            (KeyValues.CategoryGroups, true) ?? new ObservableCollection<CategoryGroup>();
        try
        {
            _developerGroup = _categoryGroups.First(cg => cg.Type == CategoryGroupType.Developer);
        }
        catch
        {
            _developerGroup = new CategoryGroup(StringExtensions.GetLocalized("CategoryService_Developer"), CategoryGroupType.Developer);
            _categoryGroups.Add(_developerGroup);
        }
        InitStatusGroup();
        
        // 将分类里的Galgame从string还原
        await Task.Run(() =>
        {
            foreach (CategoryGroup group in _categoryGroups)
            {
                foreach (Category c in group.Categories.OfType<Category>())
                    c.Galgames.ForEach(str =>
                    {
                        if (_galgameService.GetGalgameFromPath(str) is { } tmp)
                            c.Add(tmp);
                    });
            }
        });
        
        _isInit = true;
    }

    public async Task<ObservableCollection<CategoryGroup>> GetCategoryGroupsAsync()
    {
        if (_isInit == false)
            await Init();
        return _categoryGroups;
    }
    
    /// <summary>
    /// 将源分类合并到目标分类，然后删除源分类 <br/>
    /// 如果目标分类和源分类相同，则不进行任何操作
    /// </summary>
    /// <param name="target">目标分类</param>
    /// <param name="source">源分类</param>
    public void Merge(Category target, Category source)
    {
        if (target == source) return;
        target.Add(source);
        DeleteCategory(source);
    }

    /// <summary>
    /// 删除分类
    /// </summary>
    /// <param name="category">分类</param>
    public void DeleteCategory(Category category)
    {
        category.Delete();
        foreach (CategoryGroup categoryGroup in _categoryGroups)
            categoryGroup.Categories.Remove(category);
    }

    /// <summary>
    /// 更新某个分类的信息（目前只有开发商的图片）
    /// </summary>
    /// <param name="category"></param>
    public void UpdateCategory(Category category)
    {
        _queue.Add(category);
    }

    /// <summary>
    /// 更新所有游戏的分类（开发商及游玩状态）
    /// </summary>
    public async Task UpdateAllGames()
    {
        ObservableCollection<Galgame> games = await _galgameService.GetContentGridDataAsync();
        foreach (Galgame game in games)
            UpdateCategory(game);
    }

    private async void UpdateCategory(Galgame galgame)
    {
        if (_isInit == false) await Init();
        // 更新开发商分类组
        if (await _localSettings.ReadSettingAsync<bool>(KeyValues.AutoCategory) 
            && galgame.Developer.Value != Galgame.DefaultString && galgame.Developer.Value != string.Empty
            && HasDeveloperCategory(galgame) == false)
        {
            Category developer;
            try
            {
                developer = _developerGroup!.Categories.First(c =>
                    c.Name.Equals(galgame.Developer, StringComparison.OrdinalIgnoreCase));
            }
            catch
            {
                developer = new Category(galgame.Developer.Value!);
                _queue.Add(developer);
                _developerGroup!.Categories.Add(developer);
            }
            developer.Add(galgame);
        }
    }

    private async void Worker()
    {
        foreach (Category category in _queue.GetConsumingEnumerable())
        {
            var imgUrl = await _bgmPhraser.GetDeveloperImageUrlAsync(category.Name);
            if (imgUrl is null) continue;
            var imagPath = await DownloadHelper.DownloadAndSaveImageAsync(imgUrl);
            if(imagPath is not null && _dispatcher is not null)
                await _dispatcher.EnqueueAsync(() =>
                {
                    category.ImagePath = imagPath;
                });
        }
    }

    private async Task SaveAsync()
    {
        if (_isInit == false) return;
        if(_statusGroup != null)
            _categoryGroups.Remove(_statusGroup); //状态分类组是即时构造的，不需要保存
        foreach (CategoryGroup categoryGroup in _categoryGroups)
            categoryGroup.Categories.ForEach(c => c.UpdateSerializeList());
        await _localSettings.SaveSettingAsync(KeyValues.CategoryGroups, _categoryGroups, true);
    }

    /// <summary>
    /// 判断一个galgame是否已经有开发商分类了
    /// </summary>
    private bool HasDeveloperCategory(Galgame galgame)
    {
        return galgame.Categories.Any(category => _categoryGroups.Any(group =>
            group.Type == CategoryGroupType.Developer && group.Categories.Contains(category)));
    }

    /// 状态分类组是即时构造的
    private void InitStatusGroup()
    {
        _statusGroup = new CategoryGroup(ResourceExtensions.GetLocalized("CategoryService_Status"), CategoryGroupType.Status);
        _categoryGroups.Add(_statusGroup);
        _statusGroup.Categories.Add(new Category(PlayType.None.GetLocalized()));
        _statusGroup.Categories.Add(new Category(PlayType.Played.GetLocalized()));
        _statusGroup.Categories.Add(new Category(PlayType.Playing.GetLocalized()));
        _statusGroup.Categories.Add(new Category(PlayType.Shelved.GetLocalized()));
        _statusGroup.Categories.Add(new Category(PlayType.Abandoned.GetLocalized()));
    }
}