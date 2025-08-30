// SelectionMemory.cs
using System.Collections.Generic;

public static class SelectionMemory
{
    // screenId -> saved key
    private static readonly Dictionary<string, string> _byScreen = new();

    public static void Save(string screenId, string key)
    {
        if (string.IsNullOrEmpty(screenId) || string.IsNullOrEmpty(key)) return;
        _byScreen[screenId] = key;
    }

    public static bool TryGet(string screenId, out string key) => _byScreen.TryGetValue(screenId, out key);
}