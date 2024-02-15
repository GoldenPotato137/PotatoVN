using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.UI.Xaml.Controls;

namespace GalgameManager.Models;

public partial class Info : ObservableObject
{
    public bool Read;
    [ObservableProperty] private InfoBarSeverity _severity;
    [ObservableProperty] private string _title = null!;
    [ObservableProperty] private string _message = null!;
    
    public Info(InfoBarSeverity severity, string title, string message)
    {
        Severity = severity;
        Title = title;
        Message = message;
    }
}