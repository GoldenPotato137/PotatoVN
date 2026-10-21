using GalgameManager.Helpers.API.RepoFlow;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace GalgameManager.Test.Helpers;

[TestFixture]
public class RepoFlowApiModelsTest
{
    /// RepoFlow的版本列表接口在每个版本上返回downloads字段，商店据此累加总下载数
    [Test]
    public void PackageDetailVersion_DeserializesDownloads()
    {
        const string json = """
            [{"version":"0.1","versionId":"a","createdAt":"2026-02-15T03:26:50.359648+00:00","downloads":2},
             {"version":"0.2","versionId":"b","createdAt":"2026-02-16T04:19:46.04629+00:00","downloads":781}]
            """;

        List<PackageDetailVersion> versions =
            JsonConvert.DeserializeObject<List<PackageDetailVersion>>(json, new VersionConverter())!;

        Assert.That(versions.Select(v => v.Downloads), Is.EqualTo(new[] { 2L, 781L }));
    }
}
