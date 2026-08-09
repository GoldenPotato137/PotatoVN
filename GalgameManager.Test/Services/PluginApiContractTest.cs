using System.Reflection;
using GalgameManager.Enums;
using GalgameManager.Models;
using GalgameManager.Models.Sources;
using GalgameManager.Services;
using GalgameManager.WinApp.Base.Contracts;

namespace GalgameManager.Test.Services;

[TestFixture]
public class PluginApiContractTest
{
    [Test]
    public void Host_ImplementsEveryPluginApiMember()
    {
        Type apiType = typeof(IPotatoVnApi);
        Type hostType = typeof(PluginService.PotatoVnApiHost);

        Assert.Multiple(() =>
        {
            Assert.That(apiType.IsAssignableFrom(hostType), Is.True);
            Assert.That(hostType.GetInterfaceMap(apiType).InterfaceMethods,
                Has.Length.EqualTo(apiType.GetMethods().Length));
        });
    }

    [Test]
    public void Api_ExposesCompleteLibraryManagementSurface()
    {
        string[] expectedMethods =
        [
            nameof(IPotatoVnApi.GetGameByUuid),
            nameof(IPotatoVnApi.GetGameByUid),
            nameof(IPotatoVnApi.GetGameById),
            nameof(IPotatoVnApi.GetGameByName),
            nameof(IPotatoVnApi.AddVirtualGameAsync),
            nameof(IPotatoVnApi.SaveGameAsync),
            nameof(IPotatoVnApi.SaveGameMetadataAsync),
            nameof(IPotatoVnApi.RemoveGameAsync),
            nameof(IPotatoVnApi.ParseGameAsync),
            nameof(IPotatoVnApi.ParseGameInfoOnlyAsync),
            nameof(IPotatoVnApi.ParseGameCharacterAsync),
            nameof(IPotatoVnApi.ParseGameImagesAsync),
            nameof(IPotatoVnApi.GetGameInstallationConfiguration),
            nameof(IPotatoVnApi.UpdateGameInstallationAsync),
            nameof(IPotatoVnApi.SetPreferredGameInstallationAsync),
            nameof(IPotatoVnApi.RemoveGameInstallationAsync),
            nameof(IPotatoVnApi.GetAllSources),
            nameof(IPotatoVnApi.GetSourceById),
            nameof(IPotatoVnApi.GetSourceByUrl),
            nameof(IPotatoVnApi.AddSourceAsync),
            nameof(IPotatoVnApi.DeleteSourceAsync),
            nameof(IPotatoVnApi.AddGameToSource),
            nameof(IPotatoVnApi.MoveGame),
            nameof(IPotatoVnApi.GetCategoryGroupsAsync),
            nameof(IPotatoVnApi.AddCategoryGroup),
            nameof(IPotatoVnApi.DeleteCategoryGroup),
            nameof(IPotatoVnApi.SaveCategory),
            nameof(IPotatoVnApi.AddCategoryToGroup),
            nameof(IPotatoVnApi.MergeCategories),
            nameof(IPotatoVnApi.SearchFiltersAsync),
            nameof(IPotatoVnApi.SetFilter),
            nameof(IPotatoVnApi.ParseStaffAsync),
            nameof(IPotatoVnApi.ParseGameStaffAsync),
            nameof(IPotatoVnApi.DeleteStaff),
            nameof(IPotatoVnApi.InvokeOnMainThreadAsync),
        ];

        HashSet<string> actualMethods = typeof(IPotatoVnApi).GetMethods().Select(method => method.Name).ToHashSet();

        Assert.That(expectedMethods, Is.SubsetOf(actualMethods));
    }

    [Test]
    public void SourceDeletion_ExposesConfirmingAndNonConfirmingOverloads()
    {
        MethodInfo[] overloads = typeof(IPotatoVnApi).GetMethods()
            .Where(method => method.Name == nameof(IPotatoVnApi.DeleteSourceAsync))
            .ToArray();

        Assert.Multiple(() =>
        {
            Assert.That(overloads, Has.Length.EqualTo(2));
            Assert.That(overloads.Any(method => method.GetParameters().Length == 1), Is.True);
            Assert.That(overloads.Any(method => method.GetParameters() is
                [{ ParameterType: var sourceType }, { ParameterType: var removeGamesType }] &&
                sourceType == typeof(GalgameSourceBase) && removeGamesType == typeof(bool)), Is.True);
        });
    }

    [TestCase(nameof(IPotatoVnApi.RemoveGameAsync), "removeFromDisk")]
    [TestCase(nameof(IPotatoVnApi.RemoveGameInstallationAsync), "deleteFiles")]
    public void DestructiveFileOperations_DefaultToKeepingFiles(string methodName, string parameterName)
    {
        ParameterInfo parameter = typeof(IPotatoVnApi).GetMethod(methodName)!.GetParameters()
            .Single(item => item.Name == parameterName);

        Assert.That(parameter.DefaultValue, Is.False);
    }

    [Test]
    public void PluginFacingTypes_LiveInSharedSdkAssembly()
    {
        Assembly sdkAssembly = typeof(IPotatoVnApi).Assembly;

        Assert.Multiple(() =>
        {
            Assert.That(typeof(CategoryGroup).Assembly, Is.SameAs(sdkAssembly));
            Assert.That(typeof(CategoryGroupType).Assembly, Is.SameAs(sdkAssembly));
            Assert.That(typeof(GameParseType).Assembly, Is.SameAs(sdkAssembly));
        });
    }

    [Test]
    public void AppAssembly_ForwardsTypesMovedIntoSharedSdk()
    {
        Type[] forwardedTypes = typeof(PluginService).Assembly.GetForwardedTypes();

        Assert.Multiple(() =>
        {
            Assert.That(forwardedTypes, Does.Contain(typeof(CategoryGroup)));
            Assert.That(forwardedTypes, Does.Contain(typeof(CategoryGroupType)));
            Assert.That(forwardedTypes, Does.Contain(typeof(GameParseType)));
        });
    }
}
