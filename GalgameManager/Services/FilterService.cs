using System.Collections.ObjectModel;
using GalgameManager.Contracts;
using GalgameManager.Contracts.Services;
using GalgameManager.Enums;
using GalgameManager.Models;

namespace GalgameManager.Services;

public class FilterService : IFilterService
{
    public readonly ObservableCollection<IFilter> Filters;

    public FilterService(ILocalSettingsService localSettingsService)
    {
        Filters = localSettingsService.ReadSettingAsync<ObservableCollection<IFilter>>(KeyValues.Filters, true).Result
                  ?? new ObservableCollection<IFilter>();
    }

    public bool ApplyFilters(Galgame galgame)
    {
        return Filters.All(filter => filter.Apply(galgame));
    }
}