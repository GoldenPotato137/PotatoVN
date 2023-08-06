using CommunityToolkit.Mvvm.ComponentModel;
using GalgameManager.Enums;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;

namespace GalgameManager.Views.Control;

[ObservableObject]
public sealed partial class HomeItem
{
    public HomeItem()
    {
        InitializeComponent();
    }
        
    public string Image
    {
        get => (string)GetValue(ImageProperty);
        set => SetValue(ImageProperty, value);
    }

    public static readonly DependencyProperty ImageProperty =
        DependencyProperty.Register(nameof(Image), typeof(string), typeof(HomeItem), new PropertyMetadata(string.Empty));

    public string Title
    {
        get => (string)GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    public static readonly DependencyProperty TitleProperty =
        DependencyProperty.Register(nameof(Title), typeof(string), typeof(HomeItem), new PropertyMetadata(string.Empty));
    
    public Stretch Stretch
    {
        get => (Stretch)GetValue(StretchProperty);
        set => SetValue(StretchProperty, value);
    }

    public static readonly DependencyProperty StretchProperty =
        DependencyProperty.Register(nameof(Stretch), typeof(Stretch), typeof(HomeItem), new PropertyMetadata(Stretch.UniformToFill));
    
    public PlayType PlayType
    {
        get => (PlayType)GetValue(PlayTypeProperty);
        set
        {
            SetValue(PlayTypeProperty, value);
            UpdatePlayTypePolygon();
        }
    }

    public static readonly DependencyProperty PlayTypeProperty =
        DependencyProperty.Register(nameof(PlayType), typeof(PlayType), typeof(HomeItem), new PropertyMetadata(PlayType.None));
    public bool DisplayPlayType
    {
        get => (bool)GetValue(DisplayPlayTypeProperty);
        set
        {
            SetValue(DisplayPlayTypeProperty, value);
            UpdatePlayTypePolygon();
        }
    }

    private void UpdatePlayTypePolygon()
    {
        if (PlayTypePolygon is null) return;
        PlayTypePolygon.Stroke = new SolidColorBrush(PlayType.ToColor());
        PlayTypePolygon.Fill = new SolidColorBrush(PlayType.ToColor()) {Opacity = 1};
    }

    public static readonly DependencyProperty DisplayPlayTypeProperty =
        DependencyProperty.Register(nameof(DisplayPlayType), typeof(bool), typeof(HomeItem), new PropertyMetadata(false));
}