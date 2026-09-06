using GalgameManager.Helpers.Phrase;
using Microsoft.UI.Xaml.Controls;

namespace GalgameManager.Views.Dialog;

public sealed partial class MixedPhraserEnabledDialog
{
    public MixedPhraserEnabled Result { get; }

    public MixedPhraserEnabledDialog(MixedPhraserEnabled enabled)
    {
        InitializeComponent();
        XamlRoot = App.MainWindow!.Content.XamlRoot;
        Result = new MixedPhraserEnabled
        {
            BangumiEnabled = enabled.BangumiEnabled,
            VndbEnabled = enabled.VndbEnabled,
            YmgalEnabled = enabled.YmgalEnabled,
            SteamEnabled = enabled.SteamEnabled,
            HikarinagiEnabled = enabled.HikarinagiEnabled,
            NameEnabled = enabled.NameEnabled,
            DescriptionEnabled = enabled.DescriptionEnabled,
            DeveloperEnabled = enabled.DeveloperEnabled,
            EngineEnabled = enabled.EngineEnabled,
            TagsEnabled = enabled.TagsEnabled,
            RatingEnabled = enabled.RatingEnabled,
            ExpectedPlayTimeEnabled = enabled.ExpectedPlayTimeEnabled,
            ReleaseDateEnabled = enabled.ReleaseDateEnabled,
            CnNameEnabled = enabled.CnNameEnabled,
            ImageUrlEnabled = enabled.ImageUrlEnabled,
            CharactersEnabled = enabled.CharactersEnabled
        };

        // 初始化 CheckBox 状态 - Scraper sources
        BangumiCheckBox.IsChecked = enabled.BangumiEnabled;
        VndbCheckBox.IsChecked = enabled.VndbEnabled;
        YmgalCheckBox.IsChecked = enabled.YmgalEnabled;
        SteamCheckBox.IsChecked = enabled.SteamEnabled;
        HikarinagiCheckBox.IsChecked = enabled.HikarinagiEnabled;
        
        // 初始化 CheckBox 状态 - Information types
        NameCheckBox.IsChecked = enabled.NameEnabled;
        DescriptionCheckBox.IsChecked = enabled.DescriptionEnabled;
        DeveloperCheckBox.IsChecked = enabled.DeveloperEnabled;
        EngineCheckBox.IsChecked = enabled.EngineEnabled;
        TagsCheckBox.IsChecked = enabled.TagsEnabled;
        RatingCheckBox.IsChecked = enabled.RatingEnabled;
        ExpectedPlayTimeCheckBox.IsChecked = enabled.ExpectedPlayTimeEnabled;
        ReleaseDateCheckBox.IsChecked = enabled.ReleaseDateEnabled;
        CnNameCheckBox.IsChecked = enabled.CnNameEnabled;
        ImageUrlCheckBox.IsChecked = enabled.ImageUrlEnabled;
        CharactersCheckBox.IsChecked = enabled.CharactersEnabled;

        PrimaryButtonClick += OnPrimaryButtonClick;
    }

    private void OnPrimaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
    {
        // Update scraper sources
        Result.BangumiEnabled = BangumiCheckBox.IsChecked ?? false;
        Result.VndbEnabled = VndbCheckBox.IsChecked ?? false;
        Result.YmgalEnabled = YmgalCheckBox.IsChecked ?? false;
        Result.SteamEnabled = SteamCheckBox.IsChecked ?? false;
        Result.HikarinagiEnabled = HikarinagiCheckBox.IsChecked ?? false;
        
        // Update information types
        Result.NameEnabled = NameCheckBox.IsChecked ?? false;
        Result.DescriptionEnabled = DescriptionCheckBox.IsChecked ?? false;
        Result.DeveloperEnabled = DeveloperCheckBox.IsChecked ?? false;
        Result.EngineEnabled = EngineCheckBox.IsChecked ?? false;
        Result.TagsEnabled = TagsCheckBox.IsChecked ?? false;
        Result.RatingEnabled = RatingCheckBox.IsChecked ?? false;
        Result.ExpectedPlayTimeEnabled = ExpectedPlayTimeCheckBox.IsChecked ?? false;
        Result.ReleaseDateEnabled = ReleaseDateCheckBox.IsChecked ?? false;
        Result.CnNameEnabled = CnNameCheckBox.IsChecked ?? false;
        Result.ImageUrlEnabled = ImageUrlCheckBox.IsChecked ?? false;
        Result.CharactersEnabled = CharactersCheckBox.IsChecked ?? false;
    }
}
