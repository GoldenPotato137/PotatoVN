using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GalgameManager.Contracts.Services;
using GalgameManager.Contracts.ViewModels;
using GalgameManager.Enums;
using GalgameManager.Helpers;
using GalgameManager.Models;
using GalgameManager.Services;
using GalgameManager.Views.Dialog;
using Microsoft.UI.Xaml.Controls;

namespace GalgameManager.ViewModels;

public partial class AccountViewModel : ObservableRecipient, INavigationAware
{
    private readonly ILocalSettingsService _localSettingsService;
    private readonly IBgmOAuthService _bgmService;
    
    public AccountViewModel(ILocalSettingsService localSettingsService, IPvnService pvnService, 
        IBgmOAuthService bgmService)
    {
        _localSettingsService = localSettingsService;
        _pvnService = pvnService;
        _bgmService = bgmService;
    }
    
    public async void OnNavigatedTo(object parameter)
    {
        _localSettingsService.OnSettingChanged += OnLocalSettingsChanged;
        _bgmService.OnAuthResultChange += BgmAuthResultNotify;
        _pvnService.StatusChanged += HandelPvnServiceStatusChanged;
        
        _pvnServerType = await _localSettingsService.ReadSettingAsync<PvnServerType>(KeyValues.PvnServerType);
        await UpdateAccountDisplay();
    }

    public void OnNavigatedFrom()
    {
        _localSettingsService.OnSettingChanged -= OnLocalSettingsChanged;
        _bgmService.OnAuthResultChange -= BgmAuthResultNotify;
        _pvnService.StatusChanged -= HandelPvnServiceStatusChanged;
    }

    private async void OnLocalSettingsChanged(string key, object _)
    {
        if(key == KeyValues.PvnAccount)
            await UpdateAccountDisplay();
    }

    #region POTATOVN_ACCOUNT

    private readonly IPvnService _pvnService;
    [ObservableProperty] private string _pvnDisplayName = string.Empty;
    [ObservableProperty] private string _pvnStateMsg = string.Empty;
    [ObservableProperty] private string? _pvnAvatar;
    public readonly PvnServerType[] PvnServerTypes = { PvnServerType.OfficialServer, PvnServerType.CustomServer };
    [ObservableProperty] private PvnServerType _pvnServerType;
    [ObservableProperty] private string _pvnLoginButtonText = string.Empty;
    [ObservableProperty] private string _pvnLoginDescription = string.Empty;
    [ObservableProperty] private ICommand? _pvnLoginButtonCommand;
    [ObservableProperty] private bool _isPvnLogin;
    
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

    private async Task UpdateAccountDisplay()
    {
        PvnAccount? account = await _localSettingsService.ReadSettingAsync<PvnAccount>(KeyValues.PvnAccount);
        PvnAvatar = account?.Avatar;
        PvnLoginButtonText = account is null ? "Login".GetLocalized() : "Logout".GetLocalized();
        PvnLoginDescription = account is null
            ? "AccountPage_Pvn_AccountStatus_Unlogin".GetLocalized()
            : "AccountPage_Pvn_AccountStatus_Login".GetLocalized(account.Id, account.LoginMethod.GetLocalized());
        PvnLoginButtonCommand = account is null ? new RelayCommand(PvnLogin) : new RelayCommand(PvnLogout);
        PvnDisplayName = account?.UserDisplayName ?? "BgmAccount_NoName".GetLocalized();
        PvnStateMsg = "AccountPage_Pvn_ConnectTo".GetLocalized(_pvnService.BaseUri.ToString());
        IsPvnLogin = account is not null;
    }
    
    private void HandelPvnServiceStatusChanged(PvnServiceStatus status)
    {
        _ = DisplayMsgAsync(InfoBarSeverity.Informational, status.GetLocalized());
    }

    private async void PvnLogin()
    {
        try
        {
            Task<PvnServerInfo?> getAccountTask = _pvnService.GetServerInfoAsync();
            PvnLoginDialog dialog = new(getAccountTask,
                await _localSettingsService.ReadSettingAsync<string>(KeyValues.PvnAccountUserName));
            await dialog.ShowAsync();
            if (dialog.GetInfoTaskException is not null) throw dialog.GetInfoTaskException;

            if (dialog.Type != PvnLoginType.None)
                _ = DisplayMsgAsync(InfoBarSeverity.Informational, "AccountPage_Pvn_Logging".GetLocalized());
            switch (dialog.Type)
            {
                case PvnLoginType.DefaultLogin:
                    await _localSettingsService.SaveSettingAsync(KeyValues.PvnAccountUserName, dialog.UserName!);
                    await _pvnService.LoginAsync(dialog.UserName!, dialog.Password!);
                    _ = DisplayMsgAsync(InfoBarSeverity.Success, "AccountPage_Pvn_LoginSuccess".GetLocalized());
                    break;
                case PvnLoginType.DefaultRegister:
                    await _localSettingsService.SaveSettingAsync(KeyValues.PvnAccountUserName, dialog.UserName!);
                    await _pvnService.RegisterAsync(dialog.UserName!, dialog.Password!);
                    _ = DisplayMsgAsync(InfoBarSeverity.Success, "AccountPage_Pvn_RegisterSuccess".GetLocalized());
                    break;
                case PvnLoginType.Bangumi:
                    await _pvnService.LoginViaBangumiAsync();
                    _ = DisplayMsgAsync(InfoBarSeverity.Success, "AccountPage_Pvn_LoginSuccess".GetLocalized());
                    break;
            }
        }
        catch (Exception e)
        {
            _ = DisplayMsgAsync(InfoBarSeverity.Error, e.Message);
        }
    }

    private async void PvnLogout()
    {
        await _pvnService.LogOutAsync();
        _ = DisplayMsgAsync(InfoBarSeverity.Success, "AccountPage_Pvn_Logout".GetLocalized());
    }

    [RelayCommand]
    private async Task PvnSetAccount()
    {
        PvnSetAccountDialog dialog =
            new((await _localSettingsService.ReadSettingAsync<PvnAccount>(KeyValues.PvnAccount))!);
        await dialog.ShowAsync();
        if(dialog.Canceled) return;
        await _pvnService.ModifyAccountAsync(dialog.UserDisplayName, dialog.AvatarPath);
        _ = DisplayMsgAsync(InfoBarSeverity.Success, "AccountPage_Pvn_Modified".GetLocalized());
    }

    #endregion

    #region BANUGMI_ACCOUNT

    private async void BgmAuthResultNotify(OAuthResult result, string msg)
    {
        switch (result)
        {
            case OAuthResult.Done:
            case OAuthResult.Failed:
                await DisplayMsgAsync(result.ToInfoBarSeverity(), msg);
                break;
            case OAuthResult.FetchingAccount: 
            case OAuthResult.FetchingToken:
            default:
                await DisplayMsgAsync(result.ToInfoBarSeverity(), msg, 1000 * 60);
                break;
        }
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