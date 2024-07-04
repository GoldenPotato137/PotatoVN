using CommunityToolkit.Mvvm.ComponentModel;
using GalgameManager.Contracts.Services;
using GalgameManager.Enums;
using GalgameManager.Helpers;
using GalgameManager.Helpers.Converter;
using GalgameManager.Models;
using GalgameManager.Models.Sources;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace GalgameManager.Views.Dialog;

[INotifyPropertyChanged]
public sealed partial class ChangeSourceDialog
{
    public List<GalgameSourceBase> Sources { get; }
    public List<GalgameSourceBase> GalgameSources { get; }
    public bool Ok { get; private set; }
    public string TargetPath => _targetPath;
    public GalgameSourceBase MoveInSource => Sources[_selectSourceIndex];
    public GalgameSourceBase? MoveOutSource { get; private set; }
    
    [ObservableProperty] private int _selectSourceIndex;
    [ObservableProperty] private Visibility _spacePanelVisibility = Visibility.Collapsed;
    [ObservableProperty] private string _spaceInfo = string.Empty;
    [ObservableProperty] private int _spacePercent;
    [ObservableProperty] private bool _spaceShowError;
    [ObservableProperty] private Grid? _additionSettingControl;
    [ObservableProperty] private Visibility _additionSettingPanelVisibility = Visibility.Collapsed;
    [ObservableProperty] private Visibility _additionSettingVisibility = Visibility.Collapsed;
    [ObservableProperty] private Visibility _additionSettingWaitingVisibility = Visibility.Collapsed;
    [ObservableProperty] private bool _removeFromSource;
    [ObservableProperty] private int _removeFromSourceIndex;
    [ObservableProperty] private Visibility _removePanelVisibility = Visibility.Collapsed;
    [ObservableProperty] private string _moveInDescription = string.Empty;
    [ObservableProperty] private string? _moveOutDescription;
    [ObservableProperty] private Visibility _operatePanelDescriptionVisibility = Visibility.Collapsed;

    private readonly Galgame _game;
    private ChangeSourceDialogAttachSetting _attachSetting = new();
    private string _targetPath = string.Empty;
    private Task<Grid?>? _getAdditionSettingControlTask;
    private (long total, long used) _space;

    public ChangeSourceDialog(Galgame game)
    {
        InitializeComponent();
        XamlRoot = App.MainWindow!.Content.XamlRoot;
        PrimaryButtonText = "Yes".GetLocalized();
        IsPrimaryButtonEnabled = false;
        PrimaryButtonClick += (_, _) =>
        {
            Ok = true;
            MoveOutSource = RemoveFromSource ? GalgameSources![RemoveFromSourceIndex] : null;
        };
        CloseButtonText = "Cancel".GetLocalized();
        DefaultButton = ContentDialogButton.Close;

        _game = game;
        IGalgameSourceCollectionService sourceCollectionService = App.GetService<IGalgameSourceCollectionService>();
        Sources = sourceCollectionService.GetGalgameSources().ToList();
        Sources.RemoveAll(s => s.SourceType == GalgameSourceType.Virtual);
        foreach (GalgameSourceBase s in _game.Sources)
            Sources.Remove(s);

        GalgameSources = _game.Sources.ToList();
        GalgameSources.RemoveAll(s => s.SourceType is GalgameSourceType.Virtual);
    }

    async partial void OnSelectSourceIndexChanged(int value)
    {
        try
        {
            _getAdditionSettingControlTask = null;
            IsPrimaryButtonEnabled = false;
            _space = (-1, -1);
            UpdateDisplay();
            GalgameSourceBase selectedSource = Sources[value];
            _targetPath = selectedSource.Path;
            IGalgameSourceService service = SourceServiceFactory.GetSourceService(selectedSource.SourceType);
            // 空间
            SpacePanelVisibility = Visibility.Collapsed;
            _space = await service.GetSpaceAsync(selectedSource);
            // 附加设置
            _attachSetting = new();
            _attachSetting.OnValueChanged += () =>
            {
                IsPrimaryButtonEnabled = _attachSetting.OkClickable;
                _targetPath = _attachSetting.TargetPath ?? selectedSource.Path;
                UpdateDisplay();
            };
            _getAdditionSettingControlTask = service.GetAdditionSettingControlAsync(selectedSource, _attachSetting);
            UpdateDisplay();
            AdditionSettingControl = await _getAdditionSettingControlTask;
            UpdateDisplay();
        }
        catch (Exception exception)
        {
            App.GetService<IInfoService>().Event(EventType.GalgameEvent, InfoBarSeverity.Error,
                "Error during getting addition setting control", exception);
            Hide();
        }
    }

    // ReSharper disable once UnusedParameterInPartialMethod
    partial void OnRemoveFromSourceChanged(bool value) => UpdateDisplay();

    // ReSharper disable once UnusedParameterInPartialMethod
    partial void OnRemoveFromSourceIndexChanged(int value) => UpdateDisplay();

    private void UpdateDisplay()
    {
        //额外设置面板相关
        // ReSharper disable once ConditionalAccessQualifierIsNonNullableAccordingToAPIContract //假阳性
        AdditionSettingWaitingVisibility = (!_getAdditionSettingControlTask?.IsCompleted)?.ToVisibility()
                                           ?? Visibility.Collapsed;
        AdditionSettingVisibility = (AdditionSettingControl != null).ToVisibility();
        AdditionSettingPanelVisibility = (AdditionSettingVisibility == Visibility.Visible
                                          || AdditionSettingWaitingVisibility == Visibility.Visible).ToVisibility();
        //容量相关
        if (_space.total != -1 && _space.used != -1 && AdditionSettingWaitingVisibility == Visibility.Collapsed)
        {
            SpacePercent = (int)(_space.used * 100 / _space.total);
            SpaceShowError = SpacePercent >= 90;
            SpaceInfo = "ChangeSourceDialog_Space".GetLocalized(
                CapacityToStringConverter.Convert(_space.total - _space.used),
                CapacityToStringConverter.Convert(_space.total));
            SpacePanelVisibility = Visibility.Visible;
        }

        //移出源面板相关
        RemovePanelVisibility = IsPrimaryButtonEnabled.ToVisibility();
        //操作提示面板相关
        OperatePanelDescriptionVisibility = IsPrimaryButtonEnabled.ToVisibility();
        GalgameSourceBase selectedSource = Sources[SelectSourceIndex];
        MoveInDescription = SourceServiceFactory.GetSourceService(selectedSource.SourceType)
            .GetMoveInDescription(selectedSource, _targetPath);
        if (_removeFromSource)
        {
            GalgameSourceBase selectedMoveOutSource = GalgameSources[RemoveFromSourceIndex];
            MoveOutDescription = SourceServiceFactory.GetSourceService(selectedMoveOutSource.SourceType)
                .GetMoveOutDescription(selectedMoveOutSource, _game);
        }
        else
            MoveOutDescription = null;
    }
}

public class ChangeSourceDialogAttachSetting
{
    public Action? OnValueChanged { get; set; }
    private string? _targetPath;
    private bool _okClickable;

    /// 指定目标路径，若为null则表示使用源的根目录
    public string? TargetPath
    {
        get => _targetPath;
        set
        {
            _targetPath = value;
            OnValueChanged?.Invoke();
        }
    }

    /// 是否允许点击确定按钮，默认为false
    public bool OkClickable
    {
        get => _okClickable;
        set
        {
            _okClickable = value;
            OnValueChanged?.Invoke();
        }
    }
}