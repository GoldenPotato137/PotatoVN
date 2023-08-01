namespace GalgameManager.Enums;

public static class BgmOAuthConfig
{
    public const string AppId = "";
    public const string AppSecret = "";
    public const string RedirectUri = "potato-vn://bgm_oauth";

    public const string OAuthUrl = "https://bgm.tv/oauth/authorize?client_id="+AppId+"&response_type=code&redirect_uri="+RedirectUri;
}