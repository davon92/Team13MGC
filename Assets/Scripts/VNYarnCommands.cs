using UnityEngine;
using Yarn.Unity;

/// Commands you can call from Yarn, e.g.:
/// <<sfx "Select">>, <<bg "Cafe">>, <<go_to_rhythm "songId">>, <<pause_menu>>
public class VNYarnCommands : MonoBehaviour
{
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

    [YarnCommand("go_to_rhythm")]
    public void GoToRhythm(string songId = "")
    {
        // You already have SceneFlow; call the rhythm path you prefer.
        // Example:
        // SceneFlow.LoadRhythmAsync(songId).Forget();
        Debug.Log($"[Yarn] → Rhythm with song '{songId}'");
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