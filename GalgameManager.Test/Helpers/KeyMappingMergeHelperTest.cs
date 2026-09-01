using GalgameManager.Helpers;
using GalgameManager.Models;
using Windows.System;

namespace GalgameManager.Test.Helpers;

[TestFixture]
public class KeyMappingMergeHelperTest
{
    [Test]
    public void SourcesOverlap_GenericModifierOverlapsEitherPhysicalSide()
    {
        Assert.Multiple(() =>
        {
            Assert.That(KeyMappingMergeHelper.SourcesOverlap(
                [(int)VirtualKey.Shift], [(int)VirtualKey.LeftShift]), Is.True);
            Assert.That(KeyMappingMergeHelper.SourcesOverlap(
                [(int)VirtualKey.Control], [(int)VirtualKey.RightControl]), Is.True);
            Assert.That(KeyMappingMergeHelper.SourcesOverlap(
                [(int)VirtualKey.Menu], [(int)VirtualKey.LeftMenu]), Is.True);
            Assert.That(KeyMappingMergeHelper.SourcesOverlap(
                [(int)VirtualKey.LeftShift], [(int)VirtualKey.RightShift]), Is.False);
        });
    }

    [Test]
    public void BuildEffectiveMappings_LocalOverrideReplacesOverlappingGlobalRule()
    {
        KeyMapping global = Mapping(VirtualKey.Shift, VirtualKey.Enter, isGlobal: true);
        KeyMapping local = Mapping(VirtualKey.LeftShift, VirtualKey.Space);

        List<KeyMapping> result = KeyMappingMergeHelper.BuildEffectiveMappings([local], [global]);

        Assert.That(result, Has.Count.EqualTo(1));
        Assert.Multiple(() =>
        {
            Assert.That(result[0].IsGlobal, Is.False);
            Assert.That(result[0].From, Is.EqualTo(local.From));
            Assert.That(result[0].To, Is.EqualTo(local.To));
        });
    }

    [Test]
    public void BuildEffectiveMappings_OrdersInheritedGlobalsBeforeUnrelatedGameRules()
    {
        KeyMapping global = Mapping(VirtualKey.A, VirtualKey.Space, isGlobal: true);
        KeyMapping local = Mapping(VirtualKey.B, VirtualKey.Enter);

        List<KeyMapping> result = KeyMappingMergeHelper.BuildEffectiveMappings([local], [global]);

        Assert.Multiple(() =>
        {
            Assert.That(result, Has.Count.EqualTo(2));
            Assert.That(result[0].IsGlobal, Is.True);
            Assert.That(result[1].IsGlobal, Is.False);
        });
    }

    [Test]
    public void BuildPersistedGameMappings_DoesNotCopyInheritedGlobalRules()
    {
        KeyMapping inherited = Mapping(VirtualKey.A, VirtualKey.Space, isGlobal: true);
        KeyMapping disabledInherited = Mapping(VirtualKey.B, VirtualKey.Enter, isGlobal: true);
        disabledInherited.IsEnabled = false;
        KeyMapping local = Mapping(VirtualKey.C, VirtualKey.Tab);

        List<KeyMapping> result = KeyMappingMergeHelper.BuildPersistedGameMappings(
            [inherited, disabledInherited, local]);

        Assert.Multiple(() =>
        {
            Assert.That(result, Has.Count.EqualTo(2));
            Assert.That(result[0].IsGlobal, Is.True);
            Assert.That(result[0].IsEnabled, Is.False);
            Assert.That(result[0].From, Is.EqualTo(disabledInherited.From));
            Assert.That(result[0].To, Is.Empty);
            Assert.That(result[1].IsGlobal, Is.False);
            Assert.That(result[1].From, Is.EqualTo(local.From));
        });
    }

    [Test]
    public void HasDuplicateEnabledSources_DetectsGenericAndPhysicalModifierConflict()
    {
        List<KeyMapping> mappings =
        [
            Mapping(VirtualKey.Control, VirtualKey.Enter),
            Mapping(VirtualKey.RightControl, VirtualKey.Space),
        ];

        Assert.That(KeyMappingMergeHelper.HasDuplicateEnabledSources(mappings), Is.True);
    }

    [Test]
    public void SourceSignatures_KeepMouseButtonsAndKeyboardModifiersDistinct()
    {
        HashSet<string> plainMouse = KeyMappingMergeHelper.ExpandSourceSignatures([1]);
        HashSet<string> controlMouse = KeyMappingMergeHelper.ExpandSourceSignatures(
            [(int)VirtualKey.Control, 1]);
        HashSet<string> altMouse = KeyMappingMergeHelper.ExpandSourceSignatures(
            [(int)VirtualKey.Menu, 1]);

        Assert.Multiple(() =>
        {
            Assert.That(plainMouse, Is.EquivalentTo(new[] { "1" }));
            Assert.That(controlMouse, Is.EquivalentTo(new[] { "1,162", "1,163" }));
            Assert.That(altMouse, Is.EquivalentTo(new[] { "1,164", "1,165" }));
            Assert.That(controlMouse.Overlaps(plainMouse), Is.False);
            Assert.That(controlMouse.Overlaps(altMouse), Is.False);
            Assert.That(KeyMappingMergeHelper.CreateSourceSignature([(int)VirtualKey.LeftControl, 1]),
                Is.EqualTo("1,162"));
        });
    }

    [Test]
    public void SourcesOverlap_MouseChordOnlyOverlapsMatchingPhysicalModifier()
    {
        Assert.Multiple(() =>
        {
            Assert.That(KeyMappingMergeHelper.SourcesOverlap(
                [(int)VirtualKey.Control, 1], [(int)VirtualKey.LeftControl, 1]), Is.True);
            Assert.That(KeyMappingMergeHelper.SourcesOverlap(
                [(int)VirtualKey.Control, 1], [1]), Is.False);
            Assert.That(KeyMappingMergeHelper.SourcesOverlap(
                [(int)VirtualKey.Control, 1], [(int)VirtualKey.Menu, 1]), Is.False);
        });
    }

    [Test]
    public void MouseSourceKeyboardKeyIndex_IsolatesCandidatesByMouseButton()
    {
        KeyMapping controlLeft = new()
        {
            From = [(int)VirtualKey.Control, 1],
            To = [(int)VirtualKey.Enter],
            IsEnabled = true,
        };
        KeyMapping aRight = new()
        {
            From = [(int)VirtualKey.A, 2],
            To = [(int)VirtualKey.Space],
            IsEnabled = true,
        };
        KeyMapping rightWindowsMiddle = new()
        {
            From = [(int)VirtualKey.RightWindows, 3],
            To = [(int)VirtualKey.Tab],
            IsEnabled = true,
        };

        Dictionary<int, HashSet<int>> result =
            KeyMappingMergeHelper.BuildMouseSourceKeyboardKeyIndex([controlLeft, aRight, rightWindowsMiddle]);

        Assert.Multiple(() =>
        {
            Assert.That(result[1], Is.EquivalentTo(new[]
            {
                (int)VirtualKey.LeftControl,
                (int)VirtualKey.RightControl,
            }));
            Assert.That(result[1], Does.Not.Contain((int)VirtualKey.A));
            Assert.That(result[2], Is.EquivalentTo(new[] { (int)VirtualKey.A }));
            Assert.That(result[3], Is.EquivalentTo(new[] { (int)VirtualKey.LeftWindows }));
        });
    }

    [TestCase(1, 0x01)]
    [TestCase(2, 0x02)]
    [TestCase(3, 0x04)]
    [TestCase(4, 0x05)]
    [TestCase(5, 0x06)]
    [TestCase(6, 0)]
    [TestCase(7, 0)]
    public void GetMouseButtonVirtualKey_ConvertsInternalButtonCodes(int mouseButton, int expected)
    {
        Assert.That(KeyMappingMergeHelper.GetMouseButtonVirtualKey(mouseButton), Is.EqualTo(expected));
    }

    private static KeyMapping Mapping(VirtualKey from, VirtualKey to, bool isGlobal = false) => new()
    {
        From = [(int)from],
        To = [(int)to],
        IsEnabled = true,
        IsGlobal = isGlobal,
    };
}
