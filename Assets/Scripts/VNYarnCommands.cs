using UnityEngine;
using Yarn.Unity;

public class VNYarnCommands : MonoBehaviour
{
    [SerializeField] private SongDatabase songs;   // assign in Inspector

    DialogueRunner runner;

    void Awake()
    {
        runner = FindFirstObjectByType<DialogueRunner>();
        if (!runner)
        {
            Debug.LogError("[VN] No DialogueRunner found.");
            return;
        }

        // Register global command handlers (no [YarnCommand] attributes elsewhere)
        runner.AddCommandHandler<string, string>("go_to_rhythm", HandleGoToRhythm);
        runner.AddCommandHandler<string>("unlock_song", HandleUnlockSong);
        runner.AddCommandHandler<string>("unlock_gallery", HandleUnlockGallery);
    }

    // --- Commands ---

    private YarnTask HandleGoToRhythm(string songId, string returnNode)
    {
        var song = FindSong(songId);
        if (!song)
        {
            Debug.LogError($"[VN] go_to_rhythm: song '{songId}' not found.");
            return YarnTask.CompletedTask;
        }

        // Remember where VN should resume
        SceneFlow.PendingVNStartNode = returnNode;

        // Your existing scene handoff (single load, no additive ghost):
        SceneFlow.GoToRhythmFromVNAsync(song, returnNode);

        return YarnTask.CompletedTask;
    }

    private YarnTask HandleUnlockSong(string id)
    {
        PlayerPrefs.SetInt($"song_unlock.{id}", 1);
        PlayerPrefs.Save();
        Debug.Log($"[VN] unlock_song '{id}'");
        return YarnTask.CompletedTask;
    }

    private YarnTask HandleUnlockGallery(string id)
    {
        var ok = GallerySaves.Unlock(id);
        PlayerPrefs.Save();
        Debug.Log($"[VN] unlock_gallery '{id}' -> {ok}");
        return YarnTask.CompletedTask;
    }

    // --- Helpers ---

    private SongInfo FindSong(string id)
    {
        if (!songs || songs.songs == null) return null;

        foreach (var s in songs.songs)
        {
            if (!s) continue;
            var sid = string.IsNullOrEmpty(s.SongId) ? s.name : s.SongId;

            if (string.Equals(sid, id, System.StringComparison.OrdinalIgnoreCase)) return s;
            if (string.Equals(s.title, id, System.StringComparison.OrdinalIgnoreCase)) return s;
            if (string.Equals(s.name, id, System.StringComparison.OrdinalIgnoreCase)) return s;
        }
        return null;
    }
}
