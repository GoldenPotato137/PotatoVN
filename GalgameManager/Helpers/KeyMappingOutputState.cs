using Windows.System;

namespace GalgameManager.Helpers;

/// <summary>
/// Reference-counts injected keyboard and mouse output states. A physical output is
/// pressed for the first owner and released only after the last owner lets go.
/// </summary>
public sealed class KeyMappingOutputState
{
    private readonly Action<int, bool> _sendKeyboardState;
    private readonly Action<int, bool> _sendMouseState;
    private readonly Dictionary<int, int> _keyboardOwners = [];
    private readonly Dictionary<int, int> _mouseOwners = [];

    public KeyMappingOutputState(Action<int, bool> sendKeyboardState, Action<int, bool> sendMouseState)
    {
        _sendKeyboardState = sendKeyboardState;
        _sendMouseState = sendMouseState;
    }

    public void Press(KeyMappingOutput output)
    {
        foreach (int modifier in output.Modifiers)
            AddOwner(_keyboardOwners, modifier, _sendKeyboardState);
        if (output.Key is { } key)
            AddOwner(_keyboardOwners, key, _sendKeyboardState);
        if (output.MouseButton is >= 6 and <= 7)
            _sendMouseState(output.MouseButton.Value, true);
        else if (output.MouseButton is { } mouseButton)
            AddOwner(_mouseOwners, mouseButton, _sendMouseState);
    }

    public void Release(KeyMappingOutput output)
    {
        if (output.MouseButton is { } mouseButton && mouseButton is >= 1 and <= 5)
            RemoveOwner(_mouseOwners, mouseButton, _sendMouseState);
        if (output.Key is { } key)
            RemoveOwner(_keyboardOwners, key, _sendKeyboardState);
        for (int index = output.Modifiers.Count - 1; index >= 0; index--)
            RemoveOwner(_keyboardOwners, output.Modifiers[index], _sendKeyboardState);
    }

    public void Pulse(KeyMappingOutput output)
    {
        Press(output);
        Release(output);
    }

    public void Reset()
    {
        Exception? firstException = null;
        foreach (int mouseButton in _mouseOwners.Keys.ToArray())
        {
            try
            {
                _sendMouseState(mouseButton, false);
            }
            catch (Exception exception)
            {
                firstException ??= exception;
            }
        }
        foreach (int key in _keyboardOwners.Keys.ToArray())
        {
            try
            {
                _sendKeyboardState(key, false);
            }
            catch (Exception exception)
            {
                firstException ??= exception;
            }
        }
        _mouseOwners.Clear();
        _keyboardOwners.Clear();
        if (firstException is not null) throw firstException;
    }

    private static void AddOwner(IDictionary<int, int> owners, int key, Action<int, bool> sendState)
    {
        owners.TryGetValue(key, out int count);
        if (count == 0) sendState(key, true);
        owners[key] = count + 1;
    }

    private static void RemoveOwner(IDictionary<int, int> owners, int key, Action<int, bool> sendState)
    {
        if (!owners.TryGetValue(key, out int count)) return;
        if (count > 1)
        {
            owners[key] = count - 1;
            return;
        }

        sendState(key, false);
        owners.Remove(key);
    }
}

public sealed record KeyMappingOutput(
    IReadOnlyList<int> Modifiers,
    int? Key,
    int? MouseButton);

public static class KeyMappingOutputFactory
{
    public static KeyMappingOutput? Create(IReadOnlyList<int> keys)
    {
        int mouseButton = keys.FirstOrDefault(IsMouseButtonCode);
        List<VirtualKey> keyboardKeys = keys
            .Where(key => !IsMouseButtonCode(key))
            .Select(key => (VirtualKey)key)
            .Distinct()
            .OrderBy(GetKeyOrder)
            .ToList();

        List<int> modifiers = keyboardKeys
            .Where(IsModifierKey)
            .Select(key => (int)NormalizeExactModifier(key))
            .Distinct()
            .ToList();
        VirtualKey? mainKey = keyboardKeys
            .Where(key => !IsModifierKey(key))
            .Select(key => (VirtualKey?)key)
            .FirstOrDefault();

        if (mainKey is null && mouseButton == 0)
        {
            if (keyboardKeys.Count == 0) return null;
            mainKey = keyboardKeys[0];
            modifiers.Remove((int)NormalizeExactModifier(mainKey.Value));
        }

        return new KeyMappingOutput(
            modifiers,
            mainKey is null ? null : (int)mainKey.Value,
            mouseButton == 0 ? null : mouseButton);
    }

    private static bool IsMouseButtonCode(int code) => code is >= 1 and <= 7;

    private static bool IsModifierKey(VirtualKey key) => key is
        VirtualKey.Control or VirtualKey.LeftControl or VirtualKey.RightControl or
        VirtualKey.Shift or VirtualKey.LeftShift or VirtualKey.RightShift or
        VirtualKey.Menu or VirtualKey.LeftMenu or VirtualKey.RightMenu or
        VirtualKey.LeftWindows or VirtualKey.RightWindows;

    private static VirtualKey NormalizeExactModifier(VirtualKey key) => key switch
    {
        VirtualKey.RightWindows => VirtualKey.LeftWindows,
        _ => key,
    };

    private static int GetKeyOrder(VirtualKey key)
    {
        if (key is VirtualKey.LeftWindows or VirtualKey.RightWindows) return 1;
        if (key is VirtualKey.Control or VirtualKey.LeftControl or VirtualKey.RightControl) return 2;
        if (key is VirtualKey.Menu or VirtualKey.LeftMenu or VirtualKey.RightMenu) return 3;
        if (key is VirtualKey.Shift or VirtualKey.LeftShift or VirtualKey.RightShift) return 4;
        return 5;
    }
}
