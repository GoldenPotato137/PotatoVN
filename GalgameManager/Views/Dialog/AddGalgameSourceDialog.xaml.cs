using System.Collections.ObjectModel;
using System.Drawing;
using System.Runtime.InteropServices;
using GalgameManager.Enums;
using GalgameManager.Helpers;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Imaging;
using Icon = System.Drawing.Icon;

namespace GalgameManager.Views.Dialog;
public sealed partial class AddGalgameSourceDialog
{

    public List<SourceModel> SourceModels;
    public int SelectSource
    {
        get => (int)GetValue(SelectSourceProperty);
        set => SetValue(SelectSourceProperty, value);
    }
    
    public static readonly DependencyProperty SelectSourceProperty = DependencyProperty.Register(
        nameof(SelectSource),
        typeof(int),
        typeof(AddGalgameSourceDialog),
        new PropertyMetadata(0)
    );
    
    
    public AddGalgameSourceDialog()
    {
        InitializeComponent();
        SourceModels = new List<SourceModel>
        {
            new("LocalFolder", SourceType.LocalFolder),
            new("LocalZip", SourceType.LocalZip)
        };
    }
    
}

public class SourceModel
{
    public string Label { get; set; }
    public SourceType Source;
    public override string ToString()
    {
        return Label;
    }

    public SourceModel(string label, SourceType source)
    {
        Label = label;
        Source = source;
    }
}

