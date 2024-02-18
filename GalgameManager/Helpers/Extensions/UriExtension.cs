using System.Collections.Specialized;

namespace GalgameManager.Helpers;

public static class UriExtension
{
    public static Uri AddQuery(this Uri baseUri,string key, string value)
    {
        UriBuilder uriBuilder = new UriBuilder(baseUri);
        NameValueCollection query = System.Web.HttpUtility.ParseQueryString(uriBuilder.Query);
        query[key] = value;
        uriBuilder.Query = query.ToString();
        return uriBuilder.Uri;
    }
}