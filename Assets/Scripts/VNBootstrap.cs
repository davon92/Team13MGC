using UnityEngine;

public class VNBootstrap : MonoBehaviour
{
    [Tooltip("Fallback chapter if no save is queued.")]
    public string firstChapterId = "Prologue";

    async void Start()
    {
        string nodeToStart = null;

        if (SaveSystem.PendingLoadSlot != null)
        {
            var p = SaveSystem.LoadFromSlot(SaveSystem.PendingLoadSlot.Value);
            nodeToStart = string.IsNullOrEmpty(p?.chapterId) ? firstChapterId : p.chapterId;
            SaveSystem.ClearPending();
        }
        else if (SaveSystem.SlotExists(SaveSystem.AutoSlot))
        {
            // Start from checkpoint autosave if present.
            var p = SaveSystem.LoadFromSlot(SaveSystem.AutoSlot);
            nodeToStart = string.IsNullOrEmpty(p?.chapterId) ? firstChapterId : p.chapterId;
        }
        else
        {
            nodeToStart = firstChapterId;
        }

        // Small entrance fade (SceneFlow likely faded out before load)
        _ = Fade.Instance?.In(0.15f);

        var ctrl = FindObjectOfType<VNController>();
        if (ctrl != null) ctrl.StartConversation(nodeToStart);
        else Debug.LogError("[VNBootstrap] VNController not found in scene.");
    }
}