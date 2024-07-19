using GalgameManager.Contracts.Services;
using GalgameManager.Enums;
using GalgameManager.Helpers;
using GalgameManager.Helpers.API;
using Microsoft.UI.Xaml.Controls;

namespace GalgameManager.Services;

public class VndbAuthService: IVndbAuthService
{
    private VndbApi _vndbApi = new();

    private readonly ILocalSettingsService _localSettingsService;
    private readonly IInfoService _infoService;
    
    public VndbAuthService(ILocalSettingsService localSettingsService, IInfoService infoService)
    {
        _localSettingsService = localSettingsService;
        _infoService = infoService;
    }

    public async Task AuthWithToken(string token)
    {
        _vndbApi.UpdateToken(token);
        try
        {
            AuthInfoResponse authInfo = await _vndbApi.GetAuthInfo();
            VndbAccount account = new()
            {
                Id = authInfo.Id,
                Username = authInfo.Username,
                Token = token,
                Permissions = authInfo.Permissions
            };
            await _localSettingsService.SaveSettingAsync(KeyValues.VndbAccount, account);
        }
        catch (InvalidTokenException)
        {
            _infoService.Info(InfoBarSeverity.Warning, msg: "VndbAuthService_InvalidToken".GetLocalized());
            return;
        }
        catch (HttpRequestException e)
        {
            _infoService.Info(InfoBarSeverity.Warning, msg: e.Message);
            return;
        }
        _infoService.Info(InfoBarSeverity.Success, msg: "VndbAuthService_Success".GetLocalized());
    }

    public async Task LogoutAsync()
    {
        await _localSettingsService.RemoveSettingAsync(KeyValues.VndbAccount);
    }
}

public class VndbAccount
{
    public string Id="";
    public string Token="";
    public string Username = "";
    public required List<VndbApiPermission> Permissions;
}