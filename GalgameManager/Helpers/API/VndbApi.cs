using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace GalgameManager.Helpers.API;

public class VndbApi
{
    public readonly string VndbApiBaseUrl = "https://api.vndb.org/kana/";
    private static HttpClient GetHttpClient()
    {
        HttpClient httpClient = new();
        httpClient.DefaultRequestHeaders.TryAddWithoutValidation("User-Agent", "GoldenPotato/GalgameManager/1.0-dev (Windows) (https://github.com/GoldenPotato137/GalgameManager)");
        httpClient.DefaultRequestHeaders.Accept.Clear();
        httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        return httpClient;
    }

    public async Task<Stream> PostRequestInnerAsync(string query, string path)
    {
        using HttpClient httpClient = GetHttpClient();
        HttpContent content = new StringContent(query, new MediaTypeWithQualityHeaderValue("application/json"));
        HttpResponseMessage responseMessage = await httpClient.PostAsync(VndbApiBaseUrl + path, content);
        if (responseMessage.StatusCode == HttpStatusCode.TooManyRequests) throw new ThrottledException();
        responseMessage.EnsureSuccessStatusCode();
        return await responseMessage.Content.ReadAsStreamAsync();
    }

    public async Task<VndbResponse> GetVisualNovelAsync(VndbQuery vndbQuery)
    {
        var query = JsonSerializer.Serialize(vndbQuery);
        VndbResponse? vndbResponse =
            await JsonSerializer.DeserializeAsync<VndbResponse>(await PostRequestInnerAsync(query, "vn"));
        if (vndbResponse is null) throw new NullResponseException();
        return vndbResponse;
    }
    
    public class NullResponseException : Exception
    {
        public NullResponseException():base("Response is null")
        {
        }
    }
    
    public class ThrottledException : Exception
    {
        public ThrottledException():base("Throttled")
        {
        }
    }
}
