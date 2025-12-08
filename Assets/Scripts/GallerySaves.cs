// Assets/Scripts/Gallery/GallerySaves.cs
using System.Collections.Generic;
using UnityEngine;
using System.Linq;  // (top of file)
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
    
    public static void EnsureDefaults(GalleryDatabase db)
    {
        if (db == null) return;

        // If a save already exists, do nothing—respect the user's progress.
        if (PlayerPrefs.HasKey(Key)) return;

        var set = new HashSet<string>();
        foreach (var it in db.items)
        {
            if (it == null) continue;
            if (string.IsNullOrEmpty(it.id)) continue;
            if (it.unlockedByDefault) set.Add(it.id);
        }

        PlayerPrefs.SetString(Key, string.Join("|", set));
        PlayerPrefs.Save();
        _unlocks = null; // reload lazily on next access
    }

// Optional helper: unlock a batch (e.g., for cheats or debug menus)
    public static int UnlockMany(IEnumerable<string> ids)
    {
        if (ids == null) return 0;
        int added = 0;
        foreach (var id in ids.Where(s => !string.IsNullOrEmpty(s)))
        {
            if (Unlocks.Add(id)) added++;
        }
        if (added > 0)
        {
            PlayerPrefs.SetString(Key, string.Join("|", Unlocks));
            PlayerPrefs.Save();
        }
        return added;
    }

// Optional helper: wipe and reseed from a database (useful for QA)
    public static void ReseedFrom(GalleryDatabase db)
    {
        ClearAll();
        EnsureDefaults(db);
    }

}