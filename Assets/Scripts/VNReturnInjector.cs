using UnityEngine;
using Yarn.Unity;

public class VNReturnInjector : MonoBehaviour
{
    void Awake()
    {
        var runner = FindFirstObjectByType<DialogueRunner>();
        if (!runner) return;

        var store = runner.VariableStorage;

        // Inject result from rhythm, if any
        var rr = SceneFlow.PendingRhythmResult;
        if (rr != null)
        {
            store.SetValue("$lastGrade", rr.grade ?? "");
            store.SetValue("$lastScore", rr.score);
            store.SetValue("$cleared",   rr.cleared);
            store.SetValue("$maxCombo",  rr.maxCombo);
            store.SetValue("$songId",    rr.song ? (string.IsNullOrEmpty(rr.song.SongId) ? rr.song.name : rr.song.SongId) : "");
            SceneFlow.PendingRhythmResult = null;
        }

        // If a Bootstrap exists, it will choose and start the node.
        if (FindFirstObjectByType<VNBootstrap>())
            return;

        // Minimal fallback for ad-hoc test scenes without VNBootstrap:
        var resume = SceneFlow.PendingVNStartNode;
        if (!string.IsNullOrEmpty(resume))
        {
            SceneFlow.PendingVNStartNode = null;
            runner.StartDialogue(resume);
        }
        else
        {
            runner.StartDialogue("Start");
        }
    }
}