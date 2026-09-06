using System.Reflection;
using GalgameManager.Models.BgTasks;

namespace GalgameManager.Test.Models;

[TestFixture]
public class ExportTaskTest
{
    [Test]
    public void CreateFileName_IncludesTimeToSeconds()
    {
        MethodInfo method = typeof(ExportTask).GetMethod(
            "CreateFileName",
            BindingFlags.NonPublic | BindingFlags.Static)!;

        string actual = (string)method.Invoke(null, [new DateTime(2026, 8, 19, 22, 34, 31)])!;

        Assert.That(actual, Is.EqualTo("PotatoVN_26-08-19_22-34-31.pvnExport.zip"));
    }
}
