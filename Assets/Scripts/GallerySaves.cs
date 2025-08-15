// Assets/Scripts/Gallery/GallerySaves.cs
using System.Collections.Generic;
using UnityEngine;

public static class GallerySaves
{
    const string Key = "GalleryUnlocksV1";
    static HashSet<string> _unlocks;

    static HashSet<string> Unlocks
    {
        get
        {
            if (_unlocks != null) return _unlocks;
            var csv = PlayerPrefs.GetString(Key, "");
            _unlocks = new HashSet<string>(csv.Split('|'));
            _unlocks.Remove(""); // cleanup
            return _unlocks;
        }
    }

    public static bool IsUnlocked(string id) => Unlocks.Contains(id);

    public static bool Unlock(string id)
    {
        if (string.IsNullOrEmpty(id)) return false;
        if (!Unlocks.Add(id)) return false;
        PlayerPrefs.SetString(Key, string.Join("|", Unlocks));
        PlayerPrefs.Save();
        return true;
    }

    // helper for debug/QA
    public static void ClearAll()
    {
        PlayerPrefs.DeleteKey(Key);
        _unlocks = null;
    }
}