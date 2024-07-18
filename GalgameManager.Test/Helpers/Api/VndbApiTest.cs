using GalgameManager.Helpers.API;
using Refit;

namespace GalgameManager.Test.Helpers.Api;

[TestFixture]
public class VndbPhraserTest
{
    private VndbApi _vndbApi;

    [SetUp]
    public void Init()
    {
        var token = Environment.GetEnvironmentVariable("VNDB_TOKEN"); // 请在环境变量中设置 BGM_TOKEN
        _vndbApi = new VndbApi(token);
    }
    
    [Test]
    [TestCase("")]
    public async Task ApiAuthTest(string id)
    {
        //TODO
        _vndbApi.UpdateToken("zboy-yi4r1-wfo8a-6ejo-pcokq-dyoot-zyyq");
        AuthInfoResponse info = await _vndbApi.GetAuthInfo();
        try
        {
            VndbResponse<VndbUserListItem> a = await _vndbApi.GetUserVisualNovelListAsync(new VndbQuery
            {
                Fields = "vote, vn.title, labels.id, labels.label", Filters = VndbFilters.Equal("id", "v30218")
            });
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
            throw;
        }

        Assert.That(info.Id, Is.EqualTo(id));
    }
}