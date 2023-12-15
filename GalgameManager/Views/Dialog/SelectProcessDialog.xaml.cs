using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Drawing;
using GalgameManager.Helpers;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Imaging;
using Icon = System.Drawing.Icon;

namespace GalgameManager.Views.Dialog;
public sealed partial class SelectProcessDialog
{
    public string? SelectedProcessName;
    
    public SelectProcessDialog()
    {
        InitializeComponent();
        
        XamlRoot = App.MainWindow!.Content.XamlRoot;
        Title = "SelectProcessDialog_Title".GetLocalized();
        PrimaryButtonText = "Yes".GetLocalized();
        SecondaryButtonText = "Cancel".GetLocalized();
        PrimaryButtonClick += (_, _) =>
        {
            if(ListView.SelectedItem is DisplayProcess process)
                SelectedProcessName = process.Process.ProcessName;
        };
        IsPrimaryButtonEnabled = false;
        DefaultButton = ContentDialogButton.Secondary;
        
        GetProcess();
    }

    private void GetProcess()
    {
        Process[] process = Process.GetProcesses();
        ObservableCollection<DisplayProcess> processes = new();
        foreach(Process p in process)
            if (p.MainWindowHandle != IntPtr.Zero)
            {
                DisplayProcess displayProcess = new(p);
                try
                {
                    if (p.MainModule?.FileName is not null)
                    {
                        Bitmap? bitmap = Icon.ExtractAssociatedIcon(p.MainModule.FileName)?.ToBitmap();
                        if (bitmap is not null)
                        {
                            BitmapImage bitmapImage = new();
                            using (MemoryStream stream = new())
                            {
                                bitmap.Save(stream, System.Drawing.Imaging.ImageFormat.Png);
                                stream.Position = 0;
                                bitmapImage.SetSource(stream.AsRandomAccessStream());
                            }
                            displayProcess.Icon = bitmapImage;
                        }
                    }
                }
                catch
                {
                    // ignored
                }
                processes.Add(displayProcess);
            }

        ListView.ItemsSource = processes;
    }

    private void ButtonRefresh_OnClick(object sender, RoutedEventArgs e)
    {
        GetProcess();
    }

    private void ListView_OnItemClick(object sender, SelectionChangedEventArgs selectionChangedEventArgs)
    {
        IsPrimaryButtonEnabled = ListView.SelectedItem is not null;
    }
}

public class DisplayProcess
{
    public readonly Process Process;
    public BitmapImage? Icon;

    public DisplayProcess(Process process)
    {
        Process = process;
    }
}
