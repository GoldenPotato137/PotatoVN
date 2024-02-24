using System.Reflection;
using System.Runtime.InteropServices;
using GalgameManager.Contracts.Services;
using GalgameManager.Enums;
using GalgameManager.Helpers;
using GalgameManager.Views;
using Windows.Security.Credentials;
using Windows.Security.Credentials.UI;
using GalgameManager.Views.Dialog;

namespace GalgameManager.Services;

public class AuthenticationService : IAuthenticationService
{
    private readonly ILocalSettingsService _localSettingsService;

    public AuthenticationService(ILocalSettingsService localSettingsService)
    {
        _localSettingsService = localSettingsService;
    }

    public async Task<bool> StartAuthentication()
    {
        AuthenticationType authnType = await _localSettingsService.ReadSettingAsync<AuthenticationType>(KeyValues.AuthenticationType);
        if (authnType == AuthenticationType.NoAuthentication)
        {
            //无身份验证 => 直接通过
            return true;
        }
        else
        {
            if (authnType == AuthenticationType.WindowsHello)
            {
                return await StartWindowsHelloAuthentication();
            }
            else
            {
                return await StartPasswordAuthentication();
            }
        }
    }

    private static async Task<bool> StartWindowsHelloAuthentication()
    {
        var hwnd = App.MainWindow!.GetWindowHandle();
        UserConsentVerificationResult consentResult = await UserConsentVerifierInterop.RequestVerificationForWindowAsync(hwnd, "AuthenticateUserMessage".GetLocalized());

        return consentResult switch
        {
            UserConsentVerificationResult.Verified => true,
            _ => false
        };
    }

    private async Task<bool> StartPasswordAuthentication()
    {
        //等200毫秒，让XAML Root能来得及初始化
        await Task.Delay(200);

        //允许进行10次尝试，如果均失败则返回false
        for (var i = 0; i < 10; i++)
        {
            PasswordDialog passwordDialog = new()
            {
                Title = "EnterYourPasswordLiteral".GetLocalized(),
                PrimaryButtonText = "ConfirmLiteral".GetLocalized(),
                CloseButtonText = "Cancel".GetLocalized(),
                PasswordBoxPlaceholderText = "PasswordLiteral".GetLocalized(),
                Message = "ForgetPasswordMessage".GetLocalized()
            };
            await passwordDialog.ShowAsync();

            var password = passwordDialog.Password;

            if (!string.IsNullOrEmpty(password))
            {
                try
                {
                    PasswordCredential credential = new PasswordVault().Retrieve(KeyValues.CustomPasswordSaverName, KeyValues.CustomPasswordDisplayName);
                    credential.RetrievePassword();
                    var correctPassword = credential.Password;

                    if (password != correctPassword)
                    {
                        continue;
                    }
                    else
                    {
                        return true;
                    }
                }
                catch (COMException)
                {
                    //无法获取到密码时，直接返回false
                    //或许，以后我们可以优化下这里的流程？
                    return false;
                }
            }
        }

        return false;
    }
}
