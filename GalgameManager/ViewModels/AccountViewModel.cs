using System.Globalization;
using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GalgameManager.Contracts.Services;
using GalgameManager.Contracts.ViewModels;
using GalgameManager.Core.Helpers;
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
    private readonly IInfoService _infoService;
    
    public AccountViewModel(ILocalSettingsService localSettingsService, IPvnService pvnService, 
        IBgmOAuthService bgmService, IInfoService infoService)
    {
        _localSettingsService = localSettingsService;
        _pvnService = pvnService;
        _bgmService = bgmService;
        _infoService = infoService;
    }
    
    public async void OnNavigatedTo(object parameter)
    {
        _localSettingsService.OnSettingChanged += OnLocalSettingsChanged;
        _bgmService.OnAuthResultChange += BgmAuthResultNotify;
        _pvnService.StatusChanged += HandelPvnServiceStatusChanged;
        
        _pvnServerType = await _localSettingsService.ReadSettingAsync<PvnServerType>(KeyValues.PvnServerType);
        _pvnSyncGames = await _localSettingsService.ReadSettingAsync<bool>(KeyValues.SyncGames);
        await UpdateAccountDisplay();
    }

    public void OnNavigatedFrom()
    {
        _localSettingsService.OnSettingChanged -= OnLocalSettingsChanged;
        _bgmService.OnAuthResultChange -= BgmAuthResultNotify;
        _pvnService.StatusChanged -= HandelPvnServiceStatusChanged;
    }

    private async void OnLocalSettingsChanged(string key, object? value)
    {
        switch (key)
        {
            case KeyValues.PvnAccount:
            case KeyValues.BangumiAccount:
                await UpdateAccountDisplay();
                break;
            case KeyValues.SyncGames:
                PvnSyncGames = value as bool? ?? false;
                break;
        }
    }
    
    private async Task UpdateAccountDisplay()
    {
        PvnAccount? account = await _localSettingsService.ReadSettingAsync<PvnAccount>(KeyValues.PvnAccount);
        BgmAccount? bgmAccount = await _localSettingsService.ReadSettingAsync<BgmAccount>(KeyValues.BangumiAccount);
        await UiThreadInvokeHelper.InvokeAsync(() =>
        {
            PvnAvatar = account?.Avatar;
            PvnLoginButtonText = account is null ? "Login".GetLocalized() : "Logout".GetLocalized();
            PvnLoginDescription = account is null
                ? "AccountPage_Pvn_AccountStatus_Unlogin".GetLocalized()
                : "AccountPage_Pvn_AccountStatus_Login".GetLocalized(account.Id, account.LoginMethod.GetLocalized());
            PvnLoginButtonCommand = account is null ? new RelayCommand(PvnLogin) : new RelayCommand(PvnLogout);
            PvnDisplayName = account?.UserDisplayName ?? "BgmAccount_NoName".GetLocalized();
            PvnStateMsg = "AccountPage_Pvn_ConnectTo".GetLocalized(_pvnService.BaseUri.ToString());
            UsedSpace = $"{((double)(account?.UsedSpace ?? 0) / 1024 / 1024)
                .ToString("F1", CultureInfo.InvariantCulture)} MB";
            TotalSpace = $"{((double)(account?.TotalSpace ?? 0) / 1024 / 1024)
                .ToString("F1", CultureInfo.InvariantCulture)} MB";
            UsedPercentValue = (double)(account?.UsedSpace ?? 0) / (account?.TotalSpace ?? 1) * 100;
            UsedPercent = "AccountPage_Pvn_SpaceUsedPercent".GetLocalized(UsedPercentValue
                .ToString("F1", CultureInfo.InvariantCulture));
            IsPvnLogin = account is not null;
            
            BgmAccount = bgmAccount;
        });
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
    [ObservableProperty] private bool _pvnSyncGames;
    [ObservableProperty] private string? _usedSpace;
    [ObservableProperty] private string? _totalSpace;
    [ObservableProperty] private string? _usedPercent;
    [ObservableProperty] private double _usedPercentValue;
    
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
    
    private void HandelPvnServiceStatusChanged(PvnServiceStatus status)
    {
        _infoService.Info(InfoBarSeverity.Informational, msg:status.GetLocalized());
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
                _infoService.Info(InfoBarSeverity.Informational, msg: "AccountPage_Pvn_Logging".GetLocalized());
            switch (dialog.Type)
            {
                case PvnLoginType.DefaultLogin:
                    await _localSettingsService.SaveSettingAsync(KeyValues.PvnAccountUserName, dialog.UserName!);
                    await _pvnService.LoginAsync(dialog.UserName!, dialog.Password!);
                    _infoService.Info(InfoBarSeverity.Success, msg: "AccountPage_Pvn_LoginSuccess".GetLocalized());
                    break;
                case PvnLoginType.DefaultRegister:
                    await _localSettingsService.SaveSettingAsync(KeyValues.PvnAccountUserName, dialog.UserName!);
                    await _pvnService.RegisterAsync(dialog.UserName!, dialog.Password!);
                    _infoService.Info(InfoBarSeverity.Success, msg: "AccountPage_Pvn_RegisterSuccess".GetLocalized());
                    break;
                case PvnLoginType.Bangumi:
                    await _pvnService.LoginViaBangumiAsync();
                    _infoService.Info(InfoBarSeverity.Success, msg: "AccountPage_Pvn_LoginSuccess".GetLocalized());
                    break;
            }
        }
        catch (Exception e)
        {
            _infoService.Info(InfoBarSeverity.Error, msg: e.Message);
        }
    }

    private async void PvnLogout()
    {
        await _pvnService.LogOutAsync();
        _infoService.Info(InfoBarSeverity.Success, msg: "AccountPage_LogoutSuccess".GetLocalized());
    }

    [RelayCommand]
    private async Task PvnSetAccount()
    {
        PvnSetAccountDialog dialog =
            new((await _localSettingsService.ReadSettingAsync<PvnAccount>(KeyValues.PvnAccount))!);
        await dialog.ShowAsync();
        if(dialog.Canceled) return;
        try
        {
            await _pvnService.ModifyAccountAsync(dialog.UserDisplayName, dialog.AvatarPath);
            _infoService.Info(InfoBarSeverity.Success, msg: "AccountPage_Pvn_Modified".GetLocalized());
        }
        catch (Exception e)
        {
            _infoService.Info(InfoBarSeverity.Error, msg: e.ToString());
        }
    }

    partial void OnPvnSyncGamesChanged(bool value)
    {
        _localSettingsService.SaveSettingAsync(KeyValues.SyncGames, value);
        if (value)
            _pvnService.SyncGames();
    }

    #endregion

    #region BANUGMI_ACCOUNT

    public string BgmName => _bgmAccount?.Name ?? "AccountPage_Bgm_NoName".GetLocalized();
    public string? BgmAvatar => _bgmAccount?.Avatar;
    public string BgmDescription => _bgmAccount is null
        ? "AccountPage_Bgm_NoLogin".GetLocalized()
        : "AccountPage_Bgm_LoginedDescription".GetLocalized(_bgmAccount.UserId, _bgmAccount.Expires.ToStringDefault(),
            _bgmAccount.NextRefresh.ToStringDefault());
    public string BgmLoginBtnText => _bgmAccount is null ? "Login".GetLocalized() : "Logout".GetLocalized();
    public ICommand BgmLoginBtnCommand => _bgmAccount is null ? new RelayCommand(BgmLogin) : new RelayCommand(BgmLogout);
    public bool IsBgmLogin => _bgmAccount is not null;
    
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(BgmName))]
    [NotifyPropertyChangedFor(nameof(BgmAvatar))]
    [NotifyPropertyChangedFor(nameof(BgmDescription))]
    [NotifyPropertyChangedFor(nameof(BgmLoginBtnText))]
    [NotifyPropertyChangedFor(nameof(BgmLoginBtnCommand))]
    [NotifyPropertyChangedFor(nameof(IsBgmLogin))]
    private BgmAccount? _bgmAccount;

    private void BgmAuthResultNotify(BgmOAuthStatus result)
    {
        switch (result)
        {
            case BgmOAuthStatus.Failed:
                _infoService.Info(InfoBarSeverity.Error); //失败走事件通知，关闭消息栏 
                break;
            case BgmOAuthStatus.Done:
                _infoService.Info(InfoBarSeverity.Success, msg: result.GetLocalized());
                break;
            default:
                _infoService.Info(InfoBarSeverity.Informational, msg: result.GetLocalized(), displayTimeMs: 1000 * 60);
                break;
        }
    }

    private async void BgmLogin()
    {
        SelectAuthModeDialog selectAuthModeDialog = new();
        ContentDialogResult result = await selectAuthModeDialog.ShowAsync();
        if (result != ContentDialogResult.Primary) return;
        switch (selectAuthModeDialog.SelectItem)
        {
            case 0:
                await _bgmService.StartOAuthAsync();
                break;
            case 1:
                if (!string.IsNullOrEmpty(selectAuthModeDialog.AccessToken)) 
                    await _bgmService.AuthWithAccessToken(selectAuthModeDialog.AccessToken);
                break;
        }
    }

    private async void BgmLogout()
    {
        await _bgmService.LogoutAsync();
        _infoService.Info(InfoBarSeverity.Success, msg: "AccountPage_LogoutSuccess".GetLocalized());
    }

    [RelayCommand]
    private async void BgmRefreshToken()
    {
        _infoService.Info(InfoBarSeverity.Informational, msg: "AccountPage_Bgm_Refreshing".GetLocalized(),
            displayTimeMs: 1000 * 60);
        var result = await _bgmService.RefreshAccountAsync();
        if (result == false)
            _infoService.Info(InfoBarSeverity.Error); //失败走事件通知，关闭消息栏
    }
    
    #endregion
}