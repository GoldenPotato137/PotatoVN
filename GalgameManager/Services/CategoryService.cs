using System.Collections.Concurrent;
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
    private readonly Category[] _statusCategory = new Category[5];
    private bool _isInit;
    private readonly ILocalSettingsService _localSettings;
    private readonly BlockingCollection<Category> _queue = new();
    private readonly BgmPhraser _bgmPhraser;
    private readonly DispatcherQueue? _dispatcher;

    public CategoryGroup StatusGroup => _statusGroup!;

    public CategoryService(ILocalSettingsService localSettings, IDataCollectionService<Galgame> galgameService)
    {
        _localSettings = localSettings;
        _galgameService = (galgameService as GalgameCollectionService)!;
        _galgameService.GalgameAddedEvent += UpdateCategory;
        _galgameService.GalgameDeletedEvent += galgame =>
        {
            List<Category> toRemove = galgame.Categories.ToList();
            toRemove.ForEach(c => c.Remove(galgame));
        };
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

        // 有时候程序崩溃的时候没能移除游玩状态就保存了，需要手动把游玩状态移除
        List<CategoryGroup> toRemove = _categoryGroups.Where(group => group.Type == CategoryGroupType.Status).ToList();
        foreach (CategoryGroup group in toRemove)
            _categoryGroups.Remove(group);

        try
        {
            _developerGroup = _categoryGroups.First(cg => cg.Type == CategoryGroupType.Developer);
            _developerGroup.Name = ResourceExtensions.GetLocalized("CategoryService_Developer");
        }
        catch
        {
            _developerGroup = new CategoryGroup(ResourceExtensions.GetLocalized("CategoryService_Developer"), CategoryGroupType.Developer);
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
        
        foreach (Galgame g in _galgameService.Galgames) 
        {
            if (GetStatusCategory(g) == null)
                _statusCategory[(int)g.PlayType].Add(g);
            g.GalPropertyChanged += tuple =>
            {
                Galgame gal = tuple.Item1;
                switch (tuple.Item2)
                {
                    case "developer":
                        UpdateCategory(gal);
                        break;
                    case "playType":
                        GetStatusCategory(gal)?.Remove(gal);
                        _statusCategory[(int)gal.PlayType].Add(gal);
                        break;
                }
            };
        }
        
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
        //todo:空Category删除
    }

    private async void UpdateCategory(Galgame galgame)
    {
        if (_isInit == false) await Init();
        // 更新开发商分类组
        if (await _localSettings.ReadSettingAsync<bool>(KeyValues.AutoCategory) 
            && galgame.Developer.Value != Galgame.DefaultString && galgame.Developer.Value != string.Empty)
        {
            //移除旧的开发商分类
            Category? old = GetDeveloperCategory(galgame);
            old?.Remove(galgame);
            
            Category developer;
            try
            {
                Producer producer = ProducerDataHelper.Producers.First(
                    p => p.Name == galgame.Developer.Value! || 
                         p.Latin == galgame.Developer.Value! || 
                         p.Alias.Contains(galgame.Developer.Value!)
                         );
                try
                {
                    developer = _developerGroup!.Categories.First(c => c.Name.Equals(producer.Name));
                }
                catch (InvalidOperationException)
                {
                    developer = new Category(producer.Name!);
                    _queue.Add(developer);
                    _developerGroup!.Categories.Add(developer);
                }
            }
            catch (InvalidOperationException)
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
    /// 获取开发商分类，如果没有则返回null
    /// </summary>
    private Category? GetDeveloperCategory(Galgame galgame)
    {
        foreach(Category category in galgame.Categories)
            if(_developerGroup!.Categories.Contains(category))
                return category;
        return null;
    }

    /// <summary>
    /// 获取状态分类，如果没有则返回null
    /// </summary>
    private Category? GetStatusCategory(IEnumerable<Category> categories)
    {
        return categories.FirstOrDefault(category => _statusGroup!.Categories.Contains(category));
    }

    /// <summary>
    /// 获取状态分类，如果没有则返回null
    /// </summary>
    private Category? GetStatusCategory(Galgame galgame)
    {
        return GetStatusCategory(galgame.Categories);
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
        _statusCategory[(int)PlayType.None] = _statusGroup.Categories[0];
        _statusCategory[(int)PlayType.Played] = _statusGroup.Categories[1];
        _statusCategory[(int)PlayType.Playing] = _statusGroup.Categories[2];
        _statusCategory[(int)PlayType.Shelved] = _statusGroup.Categories[3];
        _statusCategory[(int)PlayType.Abandoned] = _statusGroup.Categories[4];
    }

    /// <summary>
    /// 是否在某个type的分类组中
    /// </summary>
    /// <param name="category">分类</param>
    /// <param name="type">type</param>
    public bool IsInCategoryGroup(Category category, CategoryGroupType type)
    {
        return _categoryGroups.Any(g => g.Type == type && g.Categories.Contains(category));
    }
}