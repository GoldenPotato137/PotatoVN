using GalgameManager.Helpers.Converter;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media.Imaging;

namespace GalgameManager.Views.Control;

public sealed partial class AccountPanel
{
    public AccountPanel()
    {
        InitializeComponent();
        
        UpdateAvatar(this);
    }

    public string Title
    {
        get => (string)GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    public static readonly DependencyProperty TitleProperty = DependencyProperty.Register(
        nameof(Title), typeof(string), typeof(AccountPanel),
        new PropertyMetadata(string.Empty));

    public string UserName
    {
        get => (string)GetValue(UserNameProperty);
        set => SetValue(UserNameProperty, value);
    }

    public static readonly DependencyProperty UserNameProperty = DependencyProperty.Register(
        nameof(UserName), typeof(string), typeof(AccountPanel),
        new PropertyMetadata(string.Empty));

    public string Description
    {
        get => (string)GetValue(DescriptionProperty);
        set => SetValue(DescriptionProperty, value);
    }

    public static readonly DependencyProperty DescriptionProperty = DependencyProperty.Register(
        nameof(Description), typeof(string), typeof(AccountPanel),
        new PropertyMetadata(string.Empty));

    public string Avatar
    {
        get => (string)GetValue(AvatarProperty);
        set => SetValue(AvatarProperty, value);
    }

    public static readonly DependencyProperty AvatarProperty = DependencyProperty.Register(
        nameof(Avatar), typeof(string), typeof(AccountPanel),
        new PropertyMetadata(null, OnAvatarChanged));

    private static void OnAvatarChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is AccountPanel panel) 
            UpdateAvatar(panel);
    }

    private static void UpdateAvatar(AccountPanel panel)
    {
        panel.ImageBrush.ImageSource = new ImagePathConverter().Convert(panel.Avatar, default!, 
            panel.DefaultAvatar, default!) as BitmapImage;
    }

    public string DefaultAvatar
    {
        get => (string)GetValue(DefaultAvatarProperty);
        set => SetValue(DefaultAvatarProperty, value);
    }
    
    public static readonly DependencyProperty DefaultAvatarProperty = DependencyProperty.Register(
        nameof(DefaultAvatar), typeof(string), typeof(AccountPanel),
        new PropertyMetadata("ms-appx:///Assets/Pictures/Akkarin.webp", OnAvatarChanged));

    public static readonly new DependencyProperty ContentProperty = DependencyProperty.Register(
        nameof(Content), typeof(UIElement), typeof(AccountPanel),
        new PropertyMetadata(null, OnContentChanged));

    public new UIElement Content
    {
        get => (UIElement)GetValue(ContentProperty);
        set => SetValue(ContentProperty, value);
    }

    private static void OnContentChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is AccountPanel panel)
        {
            panel.ContentArea.Content = e.NewValue;
        }
    }
    
    public bool Expand
    {
        get => (bool)GetValue(ExpandProperty);
        set => SetValue(ExpandProperty, value);
    }
    
    public static readonly DependencyProperty ExpandProperty = DependencyProperty.Register(
        nameof(Expand), typeof(bool), typeof(AccountPanel),
        new PropertyMetadata(false));
}