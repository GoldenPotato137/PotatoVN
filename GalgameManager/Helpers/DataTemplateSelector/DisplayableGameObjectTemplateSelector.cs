using System.Diagnostics;
using GalgameManager.Contracts;
using GalgameManager.Models;
using GalgameManager.Models.Sources;
using Microsoft.UI.Xaml;

namespace GalgameManager.Helpers.DataTemplateSelector;

public class DisplayableGameObjectTemplateSelector : Microsoft.UI.Xaml.Controls.DataTemplateSelector
{
    public DataTemplate GalgameTemplate { get; set; } = null!;
    public DataTemplate SourceTemplate { get; set; } = null!;

    protected override DataTemplate SelectTemplateCore(object item, DependencyObject container)
    {
        Debug.Assert(GalgameTemplate is not null && SourceTemplate is not null, "Template is not set");
        Debug.Assert(item is IDisplayableGameObject, "item is IDisplayableGameObject");
        return item switch
        {
            Galgame => GalgameTemplate,
            GalgameSourceBase => SourceTemplate,
            _ => base.SelectTemplateCore(item, container)
        };
    }
}