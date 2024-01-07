using GalgameManager.Enums;
using GalgameManager.Helpers;
using GalgameManager.Models;
using Microsoft.UI.Xaml;

namespace GalgameManager.Views.Dialog;

public sealed partial class ChangePlayStatusDialog
{
    /// <summary>
    /// 是否被取消了
    /// </summary>
    public bool Canceled;

    private readonly int[] _rateList = { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 };
    private readonly Galgame _galgame;
    public bool UploadToBgm;
    public bool UploadToVndb;
    private readonly List<string> _playStatusList = new();

    /// <summary>
    /// 修改游玩状态的对话框
    /// </summary>
    /// <param name="galgame">游戏</param>
    public ChangePlayStatusDialog(Galgame galgame)
    {
        _galgame = galgame;
        
        InitializeComponent();
        XamlRoot = App.MainWindow!.Content.XamlRoot;
        PrimaryButtonText = "Yes".GetLocalized();
        SecondaryButtonText = "Cancel".GetLocalized();
        Title = "ChangePlayStatusDialog_Title".GetLocalized();
        // DefaultButton = ContentDialogButton.Secondary;
        PrimaryButtonClick += (_, _) =>
        {
            UploadToBgm = BgmCheckBox.IsChecked ?? false;
            UploadToVndb = VndbCheckBox.IsChecked ?? false;
            _galgame.PlayType = PlayStatusBox.SelectedItem.ToString()?.CastToPlayTyped() ?? PlayType.None;
            _galgame.MyRate = RateBox.SelectedItem is int rate ? rate : 0;
            _galgame.PrivateComment = PrivateCheckBox.IsChecked ?? false;
            _galgame.Comment = CommentBox.Text;
        };
        SecondaryButtonClick += (_, _) => Canceled = true;
        Loaded += Init;

        _playStatusList.Add(PlayType.Played.GetLocalized());
        _playStatusList.Add(PlayType.Playing.GetLocalized());
        _playStatusList.Add(PlayType.Shelved.GetLocalized());
        _playStatusList.Add(PlayType.Abandoned.GetLocalized());
        PlayStatusBox.ItemsSource = _playStatusList;
        RateBox.ItemsSource = _rateList;
    }

    private void Init(object sender, RoutedEventArgs routedEventArgs)
    {
        RateBox.SelectedItem = _galgame.MyRate;
        PlayType tmp = _galgame.PlayType == PlayType.None ? PlayType.Playing : _galgame.PlayType;
        PlayStatusBox.SelectedItem = _playStatusList.First(x => x == tmp.GetLocalized());
        CommentBox.Text = _galgame.Comment;
        PrivateCheckBox.IsChecked = _galgame.PrivateComment;
    }
}