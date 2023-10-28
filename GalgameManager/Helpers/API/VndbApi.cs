using System.Net;
using System.Net.Http.Headers;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

namespace GalgameManager.Helpers.API;

public class VndbApi
{
    public readonly string VndbApiBaseUrl = "https://api.vndb.org/kana/";


    private readonly JsonSerializerSettings _jsonSerializerSettings = new()
    {
        ContractResolver = new DefaultContractResolver()
        {
            NamingStrategy = new SnakeCaseNamingStrategy()
        }
    };
    private static HttpClient GetHttpClient()
    {
        HttpClient httpClient = Utils.GetDefaultHttpClient().WithApplicationJson();
        return httpClient;
    }

    private async Task<string> PostRequestInnerAsync(string query, string path)
    {
        using HttpClient httpClient = GetHttpClient();
        HttpContent content = new StringContent(query, new MediaTypeWithQualityHeaderValue("application/json"));
        HttpResponseMessage responseMessage = await httpClient.PostAsync(VndbApiBaseUrl + path, content);
        if (responseMessage.StatusCode == HttpStatusCode.TooManyRequests) throw new ThrottledException();
        responseMessage.EnsureSuccessStatusCode();
        return await responseMessage.Content.ReadAsStringAsync();
    }

    public async Task<VndbResponse<VndbVn>> GetVisualNovelAsync(VndbQuery vndbQuery)
    {
        var query = JsonConvert.SerializeObject(vndbQuery, _jsonSerializerSettings);
        VndbResponse<VndbVn>? vndbResponse = JsonConvert.DeserializeObject<VndbResponse<VndbVn>>(await PostRequestInnerAsync(query, "vn"), _jsonSerializerSettings);
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
