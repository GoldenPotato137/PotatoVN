using System.Collections.ObjectModel;
using GalgameManager.Helpers;
using GalgameManager.Models;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace GalgameManager.Views.Dialog;

public sealed partial class EditPlayTimeDialog
{
    private readonly ObservableCollection<DisplayPlayTime> _playTimes = new();
    
    public EditPlayTimeDialog(Galgame galgame)
    {
        InitializeComponent();

        XamlRoot = App.MainWindow!.Content.XamlRoot;
        Title = "EditPlayTimeDialog_Title".GetLocalized();
        PrimaryButtonText = "Yes".GetLocalized();
        SecondaryButtonText = "Cancel".GetLocalized();
        
        foreach(var (date, playedTime) in galgame.PlayedTime)
        {
            DisplayPlayTime displayPlayTime = new()
            {
                Date = date,
                PlayedTime = playedTime
            };
            _playTimes.Add(displayPlayTime);
        }
        ListView.ItemsSource = _playTimes;
        
        PrimaryButtonClick += (_, _) =>
        {
            galgame.PlayedTime.Clear();
            var totalTime = 0;
            foreach (DisplayPlayTime time in _playTimes)
                if (time.PlayedTime > 0)
                {
                    galgame.PlayedTime.Add(time.Date, time.PlayedTime);
                    totalTime += time.PlayedTime;
                }
            galgame.TotalPlayTime = totalTime;
        };
    }

    private void DatePickerFlyout_OnDatePicked(DatePickerFlyout sender, DatePickedEventArgs args)
    {
        if (_playTimes.Any(time => time.Date == sender.Date.ToString("yyyy/MM/dd"))) return;
        DisplayPlayTime newTime = new()
        {
            Date = sender.Date.ToString("yyyy/MM/dd"),
            PlayedTime = 0
        };
        foreach (DisplayPlayTime time in _playTimes)
            if (newTime < time)
            {
                _playTimes.Insert(_playTimes.IndexOf(time), newTime);
                return;
            }
        _playTimes.Add(newTime);
    }

    private void ButtonDelete_OnClick(object sender, RoutedEventArgs e)
    {
        if (ListView.SelectedItem is not DisplayPlayTime time) return;
        _playTimes.Remove(time);
    }
}

public class DisplayPlayTime
{
    public string Date = string.Empty;
    public int PlayedTime;

    public static bool operator < (DisplayPlayTime x, DisplayPlayTime y)
    {
        var arrX = x.Date.Split('/');
        var arrY = y.Date.Split('/');
        if (int.Parse(arrX[0]) != int.Parse(arrY[0])) return int.Parse(arrX[0]) < int.Parse(arrY[0]);
        if (int.Parse(arrX[1]) != int.Parse(arrY[1])) return int.Parse(arrX[1]) < int.Parse(arrY[1]);
        return int.Parse(arrX[2]) < int.Parse(arrY[2]);
    }

    public static bool operator > (DisplayPlayTime x, DisplayPlayTime y)
    {
        return !(x < y);
    }
}
