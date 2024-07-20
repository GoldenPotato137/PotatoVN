using System.Collections.ObjectModel;
using GalgameManager.Enums;
using GalgameManager.Helpers;
using GalgameManager.Helpers.Phrase;

namespace GalgameManager.Views.Dialog;

public sealed partial class MixedPhraserOrderDialog
{
    public List<MixedPhraserOrderDialogItem> Items { get; }

    public MixedPhraserOrderDialog(MixedPhraserOrder order)
    {
        InitializeComponent();

        XamlRoot = App.MainWindow!.Content.XamlRoot;
        Title = "MixedPhraserOrderDialog_Title".GetLocalized();
        PrimaryButtonText = "Yes".GetLocalized();
        SecondaryButtonText = "Cancel".GetLocalized();

        Items = new()
        {
            new MixedPhraserOrderDialogItem("MixedPhraserOrderDialog_It_Name".GetLocalized(), order.NameOrder),
            new MixedPhraserOrderDialogItem("MixedPhraserOrderDialog_It_Des".GetLocalized(), order.DescriptionOrder),
            new MixedPhraserOrderDialogItem("MixedPhraserOrderDialog_It_Exp".GetLocalized(), order.ExpectedPlayTimeOrder),
            new MixedPhraserOrderDialogItem("MixedPhraserOrderDialog_It_Rating".GetLocalized(), order.RatingOrder),
            new MixedPhraserOrderDialogItem("MixedPhraserOrderDialog_It_Image".GetLocalized(), order.ImageUrlOrder),
            new MixedPhraserOrderDialogItem("MixedPhraserOrderDialog_It_ReleaseDate".GetLocalized(), order.ReleaseDateOrder),
            new MixedPhraserOrderDialogItem("MixedPhraserOrderDialog_It_Character".GetLocalized(), order.CharactersOrder),
            new MixedPhraserOrderDialogItem("MixedPhraserOrderDialog_It_CnName".GetLocalized(), order.CnNameOrder),
            new MixedPhraserOrderDialogItem("MixedPhraserOrderDialog_It_Dev".GetLocalized(), order.DeveloperOrder),
            new MixedPhraserOrderDialogItem("MixedPhraserOrderDialog_It_Tag".GetLocalized(), order.TagsOrder),
        };
    }
}

public class MixedPhraserOrderDialogItem
{
    public string Title { get; }
    public ObservableCollection<RssType> Order { get; }

    public MixedPhraserOrderDialogItem(string title, ObservableCollection<RssType> order)
    {
        Title = title;
        Order = order;
    }
}