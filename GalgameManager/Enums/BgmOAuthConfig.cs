namespace GalgameManager.Enums;

public static class BgmOAuthConfig
{
    public const string AppId = "bgm273264c1e79e6c30c";
    public const string AppSecret = "6aaad2643c4abfc6393860262b092338";
    public const string RedirectUri = "potato-vn://bgm_oauth";

    public const string OAuthUrl = "https://bgm.tv/oauth/authorize?client_id="+AppId+"&response_type=code&redirect_uri="+RedirectUri;
}