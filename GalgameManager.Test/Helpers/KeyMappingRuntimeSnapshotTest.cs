using GalgameManager.Helpers;
using GalgameManager.Models;
using GalgameManager.WinApp.Base.Models.Msgs;
using Windows.System;

namespace GalgameManager.Test.Helpers;

[TestFixture]
public class KeyMappingRuntimeSnapshotTest
{
    [Test]
    public void BuildEffectiveMappings_PreservesExistingOptInSemantics()
    {
        KeyMapping game = Mapping(VirtualKey.A, VirtualKey.Space);
        KeyMapping global = Mapping(VirtualKey.B, VirtualKey.Enter, isGlobal: true);

        List<KeyMapping> none = KeyMappingRuntimeSnapshot.BuildEffectiveMappings([game], [global], false, false);
        List<KeyMapping> gameOnly = KeyMappingRuntimeSnapshot.BuildEffectiveMappings([game], [global], true, false);
        List<KeyMapping> globalOnly = KeyMappingRuntimeSnapshot.BuildEffectiveMappings([game], [global], false, true);
        List<KeyMapping> both = KeyMappingRuntimeSnapshot.BuildEffectiveMappings([game], [global], true, true);

        Assert.Multiple(() =>
        {
            Assert.That(none, Is.Empty);
            Assert.That(gameOnly.Select(item => item.From.Single()),
                Is.EquivalentTo(new[] { (int)VirtualKey.A, (int)VirtualKey.B }));
            Assert.That(globalOnly.Select(item => item.From.Single()),
                Is.EquivalentTo(new[] { (int)VirtualKey.A, (int)VirtualKey.B }));
            Assert.That(both.Select(item => item.From.Single()),
                Is.EquivalentTo(new[] { (int)VirtualKey.A, (int)VirtualKey.B }));
        });
    }

    [Test]
    public void Create_ClonesRulesBeforePublishingSnapshot()
    {
        KeyMapping mapping = Mapping(VirtualKey.A, VirtualKey.Space);
        KeyMappingRuntimeSnapshot snapshot = KeyMappingRuntimeSnapshot.Create([mapping]);
        string signature = KeyMappingMergeHelper.CreateSourceSignature([(int)VirtualKey.A]);

        mapping.From[0] = (int)VirtualKey.B;
        mapping.To[0] = (int)VirtualKey.Enter;

        Assert.Multiple(() =>
        {
            Assert.That(snapshot.LookupMap.ContainsKey(signature), Is.True);
            Assert.That(snapshot.LookupMap[signature].Key, Is.EqualTo((int)VirtualKey.Space));
        });
    }

    [Test]
    public void HeldInputGuard_RequiresKeyboardAndMouseReleaseAfterTransition()
    {
        KeyMappingHeldInputGuard guard = new();
        guard.BeginTransition([(int)VirtualKey.LeftControl, (int)VirtualKey.A], [1]);

        Assert.Multiple(() =>
        {
            Assert.That(guard.IsKeyboardSuppressed([(int)VirtualKey.LeftControl, (int)VirtualKey.B]), Is.True);
            Assert.That(guard.IsMouseSuppressed(1, []), Is.True);
            Assert.That(guard.IsMouseSuppressed(2, [(int)VirtualKey.LeftControl]), Is.True);
        });

        guard.ReleaseKeyboard((int)VirtualKey.LeftControl);
        guard.ReleaseKeyboard((int)VirtualKey.A);
        guard.ReleaseMouse(1);

        Assert.Multiple(() =>
        {
            Assert.That(guard.IsKeyboardSuppressed([(int)VirtualKey.LeftControl, (int)VirtualKey.B]), Is.False);
            Assert.That(guard.IsMouseSuppressed(1, []), Is.False);
            Assert.That(guard.IsMouseSuppressed(2, [(int)VirtualKey.LeftControl]), Is.False);
        });
    }

    [Test]
    public void KeyMappingsChangedMessage_CapturesSavedRulesInsteadOfSharingMutableGalgameState()
    {
        Galgame galgame = new()
        {
            KeyReMap = true,
            KeyMappings = [Mapping(VirtualKey.A, VirtualKey.Space)],
        };

        KeyMappingsChangedMessage message = new(galgame);
        galgame.KeyReMap = false;
        galgame.KeyMappings[0].From[0] = (int)VirtualKey.B;
        galgame.KeyMappings[0].To[0] = (int)VirtualKey.Enter;

        Assert.Multiple(() =>
        {
            Assert.That(message.GalgameUuid, Is.EqualTo(galgame.Uuid));
            Assert.That(message.GameMappingOptInEnabled, Is.True);
            Assert.That(message.GameMappings.Single().From.Single(), Is.EqualTo((int)VirtualKey.A));
            Assert.That(message.GameMappings.Single().To.Single(), Is.EqualTo((int)VirtualKey.Space));
        });
    }

    private static KeyMapping Mapping(VirtualKey from, VirtualKey to, bool isGlobal = false) => new()
    {
        From = [(int)from],
        To = [(int)to],
        IsEnabled = true,
        IsGlobal = isGlobal,
    };
}
