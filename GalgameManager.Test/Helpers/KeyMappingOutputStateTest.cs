using GalgameManager.Helpers;
using Windows.System;

namespace GalgameManager.Test.Helpers;

[TestFixture]
public class KeyMappingOutputStateTest
{
    [Test]
    public void Create_ModifierAndMouseTargetPreservesBothParts()
    {
        KeyMappingOutput? output = KeyMappingOutputFactory.Create(
            [(int)VirtualKey.LeftControl, 1]);

        Assert.That(output, Is.Not.Null);
        Assert.Multiple(() =>
        {
            Assert.That(output!.Modifiers, Is.EqualTo(new[] { (int)VirtualKey.LeftControl }));
            Assert.That(output.Key, Is.Null);
            Assert.That(output.MouseButton, Is.EqualTo(1));
        });
    }

    [Test]
    public void SharedKeyboardTargetIsReleasedAfterLastOwner()
    {
        List<string> events = [];
        KeyMappingOutputState state = CreateState(events);
        KeyMappingOutput output = new([], (int)VirtualKey.Enter, null);

        state.Press(output);
        state.Press(output);
        state.Release(output);
        state.Release(output);

        Assert.That(events, Is.EqualTo(new[]
        {
            $"key:{(int)VirtualKey.Enter}:down",
            $"key:{(int)VirtualKey.Enter}:up",
        }));
    }

    [Test]
    public void SharedModifierIsReleasedAfterBothTargets()
    {
        List<string> events = [];
        KeyMappingOutputState state = CreateState(events);
        KeyMappingOutput first = new(
            [(int)VirtualKey.LeftControl], (int)VirtualKey.C, null);
        KeyMappingOutput second = new(
            [(int)VirtualKey.LeftControl], (int)VirtualKey.V, null);

        state.Press(first);
        state.Press(second);
        state.Release(first);
        state.Release(second);

        Assert.That(events, Is.EqualTo(new[]
        {
            $"key:{(int)VirtualKey.LeftControl}:down",
            $"key:{(int)VirtualKey.C}:down",
            $"key:{(int)VirtualKey.V}:down",
            $"key:{(int)VirtualKey.C}:up",
            $"key:{(int)VirtualKey.V}:up",
            $"key:{(int)VirtualKey.LeftControl}:up",
        }));
    }

    [Test]
    public void ModifierAndMouseTargetUsesKeyboardAndMouseStateTogether()
    {
        List<string> events = [];
        KeyMappingOutputState state = CreateState(events);
        KeyMappingOutput output = new([(int)VirtualKey.LeftControl], null, 1);

        state.Press(output);
        state.Release(output);

        Assert.That(events, Is.EqualTo(new[]
        {
            $"key:{(int)VirtualKey.LeftControl}:down",
            "mouse:1:down",
            "mouse:1:up",
            $"key:{(int)VirtualKey.LeftControl}:up",
        }));
    }

    [Test]
    public void SharedMouseTargetIsReleasedAfterLastOwner()
    {
        List<string> events = [];
        KeyMappingOutputState state = CreateState(events);
        KeyMappingOutput output = new([], null, 1);

        state.Press(output);
        state.Press(output);
        state.Release(output);
        state.Release(output);

        Assert.That(events, Is.EqualTo(new[]
        {
            "mouse:1:down",
            "mouse:1:up",
        }));
    }

    private static KeyMappingOutputState CreateState(ICollection<string> events) => new(
        (key, down) => events.Add($"key:{key}:{(down ? "down" : "up")}"),
        (button, down) => events.Add($"mouse:{button}:{(down ? "down" : "up")}"));
}
