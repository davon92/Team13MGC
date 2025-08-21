using System;
using System.IO;
using UnityEngine;

public static class SaveSystem
{
    // Slots: 0 is Auto, 1..(MaxSlots-1) are player "quick save" slots.
    public const int AutoSlot = 0;
    public const int MaxSlots = 6; // 0..5 (adjust to taste)

    public static int CurrentSlot { get; private set; } = -1;

    // Pending intents (used when switching scenes)
    public static bool  PendingNewGame   { get; private set; }
    public static int?  PendingLoadSlot  { get; private set; }

    [Serializable]
    public class ProfileData
    {
        public string chapterId;         // where to resume VN (node name, checkpoint id, etc.)
        public string note;              // e.g. "Auto Save", "Manual Save"
        public long   savedAtUtcTicks;   // when the save was made

        // OPTIONAL: add what you need to resume *exactly* (save-state style).
        // e.g. Yarn vars snapshot JSON, node position, choice stack…
        // public string yarnVariablesJson;
        // public string yarnCheckpointId;
    }

    static string Root => Path.Combine(Application.persistentDataPath, "Saves");
    static string PathFor(int slot) => Path.Combine(Root, $"slot_{slot}.json");

    public static void EnsureDir()
    {
        if (!Directory.Exists(Root)) Directory.CreateDirectory(Root);
    }

    public static bool SlotExists(int slot) {
        EnsureDir();
        return File.Exists(PathFor(slot));
    }

    public static ProfileData LoadFromSlot(int slot)
    {
        EnsureDir();
        var path = PathFor(slot);
        if (!File.Exists(path)) return null;
        try
        {
            var json = File.ReadAllText(path);
            return JsonUtility.FromJson<ProfileData>(json);
        }
        catch { return null; }
    }

    public static void SaveToSlot(int slot, ProfileData data)
    {
        EnsureDir();
        data.savedAtUtcTicks = DateTime.UtcNow.Ticks;
        var json = JsonUtility.ToJson(data, prettyPrint: true);
        File.WriteAllText(PathFor(slot), json);
        CurrentSlot = slot;
    }

    /// Create/overwrite autosave with a brand new game start.
    public static void CreateNewGame(string firstChapterId = "Prologue")
    {
        var p = new ProfileData
        {
            chapterId = firstChapterId,
            note = "Auto Save (New Game)"
        };
        SaveToSlot(AutoSlot, p);
        QueueLoadSlot(AutoSlot);
        PendingNewGame = true; // gives VNBootstrap a hint if you want different behavior
    }

    public static void QueueLoadSlot(int slot)     { PendingLoadSlot = slot; }
    public static void ClearPending()              { PendingNewGame = false; PendingLoadSlot = null; }

    public static string ToDateString(long utcTicks)
    {
        if (utcTicks <= 0) return "";
        var dt = new DateTime(utcTicks, DateTimeKind.Utc).ToLocalTime();
        return dt.ToString("yyyy-MM-dd HH:mm");
    }

    // ---------- Quick-load helpers ----------

    /// Returns the manual slot (1..MaxSlots-1) with the most recent timestamp, or -1 if none.
    public static int GetMostRecentManualSlot()
    {
        long bestTicks = 0;
        int bestSlot = -1;
        for (int s = 1; s < MaxSlots; s++)
        {
            var p = LoadFromSlot(s);
            if (p == null) continue;
            if (p.savedAtUtcTicks > bestTicks)
            {
                bestTicks = p.savedAtUtcTicks;
                bestSlot = s;
            }
        }
        return bestSlot;
    }

    /// Queues the most recent manual save for loading (handy for a "Quick Load" button).
    public static bool QueueQuickLoadMostRecent()
    {
        int s = GetMostRecentManualSlot();
        if (s < 0) return false;
        QueueLoadSlot(s);
        return true;
    }
}
