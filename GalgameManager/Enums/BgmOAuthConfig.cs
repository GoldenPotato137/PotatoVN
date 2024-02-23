namespace GalgameManager.Enums;

public static class BgmOAuthConfig
{
    public const string Host = "oauth-bgm";

    public const string AppId = "bgm26036422a855d5849";
    public const string RedirectUri = $"potato-vn://{Host}";

    public const string OAuthUrl = "https://bgm.tv/oauth/authorize?client_id="+AppId+"&response_type=code&redirect_uri="+RedirectUri;
}