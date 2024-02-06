using CommunityToolkit.Mvvm.ComponentModel;
using GalgameManager.Contracts.Services;
using GalgameManager.Contracts.ViewModels;
using GalgameManager.Enums;
using GalgameManager.Views.Dialog;
using Microsoft.UI.Xaml.Controls;

namespace GalgameManager.ViewModels;

public partial class AccountViewModel : ObservableRecipient, INavigationAware
{
    private readonly ILocalSettingsService _localSettingsService;
    
    public AccountViewModel(ILocalSettingsService localSettingsService)
    {
        _localSettingsService = localSettingsService;
    }
    
    public async void OnNavigatedTo(object parameter)
    {
        _pvnServerType = await _localSettingsService.ReadSettingAsync<PvnServerType>(KeyValues.PvnServerType);
    }

    public void OnNavigatedFrom()
    {
    }

    #region PotatoVN_Account

    [ObservableProperty] private string _pvnDisplayName = "GoldenPotato137";
    [ObservableProperty] private string _pvnStateMsg = "连接至：https://vn.goldenpotato.cn";
    [ObservableProperty] private string? _pvnAvatar;
    public readonly PvnServerType[] PvnServerTypes = { PvnServerType.OfficialServer, PvnServerType.CustomServer };
    [ObservableProperty] private PvnServerType _pvnServerType;
    
    async partial void OnPvnServerTypeChanged(PvnServerType value)
    {
        if (value == PvnServerType.CustomServer && await TrySetCustomServer() == false)
        {
            PvnServerType = PvnServerType.OfficialServer;
            return;
        }

        await _localSettingsService.SaveSettingAsync(KeyValues.PvnServerType, value);
    }

    private async Task<bool> TrySetCustomServer()
    {
        SelectPvnServerDialog dialog = new();
        await dialog.ShowAsync();
        if (string.IsNullOrEmpty(dialog.ServerUrl))
            return false;
        await _localSettingsService.SaveSettingAsync(KeyValues.PvnServerEndpoint, dialog.ServerUrl);
        return true;
    }

    #endregion

    #region INFO_BAR_CTRL

    private int _infoBarIndex;
    [ObservableProperty] private bool _isInfoBarOpen;
    [ObservableProperty] private string _infoBarMessage = string.Empty;
    [ObservableProperty] private InfoBarSeverity _infoBarSeverity = InfoBarSeverity.Informational;

    /// <summary>
    /// 使用InfoBar显示信息
    /// </summary>
    /// <param name="infoBarSeverity">信息严重程度</param>
    /// <param name="msg">信息</param>
    /// <param name="delayMs">显示时长(ms)</param>
    private async Task DisplayMsgAsync(InfoBarSeverity infoBarSeverity, string msg, int delayMs = 3000)
    {
        var currentIndex = ++_infoBarIndex;
        InfoBarSeverity = infoBarSeverity;
        InfoBarMessage = msg;
        IsInfoBarOpen = true;
        await Task.Delay(delayMs);
        if (currentIndex == _infoBarIndex)
            IsInfoBarOpen = false;
    }

    #endregion
}