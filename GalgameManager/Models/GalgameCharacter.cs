using CommunityToolkit.Mvvm.ComponentModel;
using Newtonsoft.Json;

namespace GalgameManager.Models;

public partial class GalgameCharacter: ObservableObject
{
    [ObservableProperty] private string _name = "";
    [ObservableProperty] private string _relation = "";
    [ObservableProperty] private string _previewImagePath = Galgame.DefaultImagePath;
    [ObservableProperty] private string _imagePath = Galgame.DefaultImagePath;
    [ObservableProperty] private string _summary = "";
    [ObservableProperty] private Gender _gender = Models.Gender.Unknown;
    [ObservableProperty] private int? _birthYear;
    [ObservableProperty] private int? _birthMon;
    [ObservableProperty] private int? _birthDay;
    [ObservableProperty] private string? _birthDate;
    [ObservableProperty] private string? _bloodType;
    [ObservableProperty] private string? _height;
    [ObservableProperty] private string? _weight;
    [ObservableProperty] private string? _bWH;
    [JsonIgnore] public string? PreviewImageUrl;
    [JsonIgnore] public string? ImageUrl;
}

public enum Gender
{
    Unknown,
    Male,
    Female
}