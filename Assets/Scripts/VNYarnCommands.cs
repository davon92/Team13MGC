using UnityEngine;
using Yarn.Unity;

/// Commands you can call from Yarn, e.g.:
/// <<sfx "Select">>, <<bg "Cafe">>, <<go_to_rhythm "songId">>, <<pause_menu>>
public class VNYarnCommands : MonoBehaviour
{
    [SerializeField] SongDatabase songDb;
    
    SongInfo Find(string id)
    {
        if (!songDb || songDb.songs == null) return null;
        foreach (var s in songDb.songs) {
            if (!s) continue;
            if (string.Equals(s.name,  id, System.StringComparison.OrdinalIgnoreCase)) return s;
            if (string.Equals(s.title, id, System.StringComparison.OrdinalIgnoreCase)) return s;
        }
        return null;
    }

    
    [YarnCommand("sfx")]
    public void PlaySfx(string clipName)
    {
        // Hook into your SFX system
        // e.g. AudioHub.Play(clipName);
        Debug.Log($"[Yarn] SFX: {clipName}");
    }

    [YarnCommand("bg")]
    public void SetBackground(string bgId)
    {
        // Swap a background sprite, play a transition, etc.
        Debug.Log($"[Yarn] BG → {bgId}");
    }

    // <<go_to_rhythm "songId" "ReturnNode">>
    [YarnCommand("go_to_rhythm")]
    public void GoToRhythm(string songId, string returnNode = null)
    {
        var song = Find(songId);
        if (!song) { Debug.LogError($"[Yarn] Song id '{songId}' not found."); return; }
        _ = SceneFlow.GoToRhythmFromVNAsync(song, returnNode);
    }

    // Optional helper: <<resume "NodeName">>
    [YarnCommand("resume")]
    public void Resume(string nodeName)
    {
        SceneFlow.PendingVNStartNode = nodeName;
    }

    [YarnCommand("pause_menu")]
    public void PauseMenu()
    {
        // If your VN has a ScreenController in-scene, push the pause screen ID.
        // ScreenController.Instance?.Push(MenuIds.VNPause);
        Debug.Log("[Yarn] Pause menu requested");
    }

    [YarnCommand("unlock_gallery")]
    public void UnlockGallery(string id)
    {
        // Call your gallery unlock system
        Debug.Log($"[Yarn] Unlock gallery: {id}");
    }

    [YarnCommand("unlock_song")]
    public void UnlockSong(string id)
    {
        // Call your song unlock system
        Debug.Log($"[Yarn] Unlock song: {id}");
    }
}