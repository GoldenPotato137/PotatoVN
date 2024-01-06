using System.Collections.ObjectModel;
using System.Drawing;
using System.Runtime.InteropServices;
using GalgameManager.Helpers;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Imaging;
using Icon = System.Drawing.Icon;

namespace GalgameManager.Views.Dialog;
public sealed partial class SelectFileDialog
{
    public string? SelectedFilePath;
    public bool RememberMe;
    private readonly IEnumerable<string> _fileExtensions;
    private readonly string _path;
    
    public SelectFileDialog(string path,IEnumerable<string> fileExtensions, string title, bool displayRememberMe = true)
    {
        InitializeComponent();

        _path = path;
        _fileExtensions = fileExtensions;
        RememberMeCheckBox.Visibility = displayRememberMe ? Visibility.Visible : Visibility.Collapsed;
       
        XamlRoot = App.MainWindow!.Content.XamlRoot;
        Title = title;
        PrimaryButtonText = "Yes".GetLocalized();
        SecondaryButtonText = "Cancel".GetLocalized();
        PrimaryButtonClick += (_, _) =>
        {
            if(ListView.SelectedItem is DisplayFile file)
                SelectedFilePath = file.Path;
        };
        SecondaryButtonClick += (_, _) =>
        {
            SelectedFilePath = null;
        };
        IsPrimaryButtonEnabled = false;
        DefaultButton = ContentDialogButton.Secondary;
        
        GetFiles();
    }

    private void GetFiles()
    {
        List<string> result = new();
        foreach(var ext in _fileExtensions)
            result.AddRange(Directory.GetFiles(_path).Where(file => file.ToLower().EndsWith(ext)));
        
        ObservableCollection<DisplayFile> files = new();
        foreach (var file in result)
        {
            DisplayFile displayFile = new(file);
            try
            {
                Bitmap bitmap = IconExtractor.GetFileIcon(file, false).ToBitmap();
                BitmapImage bitmapImage = new();
                using (MemoryStream stream = new())
                {
                    bitmap.Save(stream, System.Drawing.Imaging.ImageFormat.Png);
                    stream.Position = 0;
                    bitmapImage.SetSource(stream.AsRandomAccessStream());
                }

                displayFile.Icon = bitmapImage;
            }
            catch (Exception)
            {
                //ignore
            }
            files.Add(displayFile);
        }

        NotFoundText.Visibility = result.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        NotFoundText.Text = "SelectFileDialog_NoFound".GetLocalized(_path, string.Join(",", _fileExtensions));
        ListView.Visibility = result.Count == 0 ? Visibility.Collapsed : Visibility.Visible;
        ListView.ItemsSource = files;
    }

    private void ListView_OnItemClick(object sender, SelectionChangedEventArgs selectionChangedEventArgs)
    {
        IsPrimaryButtonEnabled = ListView.SelectedItem is not null;
    }
}

public class DisplayFile
{
    public readonly string Path;
    public readonly string DisplayText;
    public BitmapImage? Icon;

    public DisplayFile(string path)
    {
        Path = path;
        DisplayText = new FileInfo(path).Name;
    }
}

public static class IconExtractor
{
    [DllImport("shell32.dll", CharSet = CharSet.Auto)]
    public static extern IntPtr SHGetFileInfo(string pszPath, uint dwFileAttributes, ref SHFILEINFO psfi, uint cbSizeFileInfo, uint uFlags);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
    public struct SHFILEINFO
    {
        public IntPtr hIcon;
        public int iIcon;
        public uint dwAttributes;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
        public string szDisplayName;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 80)]
        public string szTypeName;
    };

    public const uint SHGFI_ICON = 0x100;
    public const uint SHGFI_LARGEICON = 0x0;    // 'Large icon
    public const uint SHGFI_SMALLICON = 0x1;    // 'Small icon

    public static Icon GetFileIcon(string fileName, bool smallIcon)
    {
        SHFILEINFO shinfo = new SHFILEINFO();
        var flags = SHGFI_ICON | (smallIcon ? SHGFI_SMALLICON : SHGFI_LARGEICON);

        SHGetFileInfo(fileName, 0, ref shinfo, (uint)Marshal.SizeOf(shinfo), flags);

        Icon icon = (Icon)Icon.FromHandle(shinfo.hIcon).Clone();
        User32.DestroyIcon(shinfo.hIcon); // 释放图标句柄
        return icon;
    }
}

public static class User32
{
    [DllImport("user32.dll", SetLastError = true)]
    public static extern bool DestroyIcon(IntPtr hIcon);
}