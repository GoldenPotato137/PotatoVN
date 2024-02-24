using CommunityToolkit.Mvvm.ComponentModel;
using Newtonsoft.Json;

namespace GalgameManager.Models;

public partial class GalgameCharacter: ObservableObject
{
    [ObservableProperty] private string _name = "";
    [ObservableProperty] private string _relation = "";
    [ObservableProperty] private string _imagePath = Galgame.DefaultImagePath;
    [JsonIgnore] public string? ImageUrl;
}