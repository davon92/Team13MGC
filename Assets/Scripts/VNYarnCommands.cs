using System.Collections;
using UnityEngine;
using Yarn.Unity;

public class VNYarnCommands : MonoBehaviour
{
    [SerializeField] private SongDatabase songs;   // assign in Inspector
    [SerializeField] VNStage stage;
    DialogueRunner runner;
    static bool _registered;
    void Awake()
    {
        if (!runner) runner = FindFirstObjectByType<DialogueRunner>();
        if (!stage)  stage  = FindFirstObjectByType<VNStage>();
        runner.AddCommandHandler("bg",   (string[] a) => StartCoroutine(BgVarArgs(a)));
        runner.AddCommandHandler("show", (string[] a) => StartCoroutine(ShowVarArgs(a)));
        runner.AddCommandHandler("hide", (string[] a) => StartCoroutine(HideVarArgs(a)));
        runner.AddCommandHandler("move", (string[] a) => StartCoroutine(MoveVarArgs(a)));
        runner.AddCommandHandler("expr", (string[] a) => StartCoroutine(ExprVarArgs(a)));
        runner.AddCommandHandler("flip", (string[] a) => StartCoroutine(FlipVarArgs(a)));
        runner.AddCommandHandler<string, string>("go_to_rhythm", HandleGoToRhythm);
        runner.AddCommandHandler<string>("unlock_song", HandleUnlockSong);
        runner.AddCommandHandler<string>("unlock_gallery", HandleUnlockGallery);
        
        runner.AddCommandHandler("vn_autosave", () => {
            var pd = new SaveSystem.ProfileData {
                chapterId = VNSaveManager.LastNode,
                note = "Auto Save",
                yarnVariablesJson = VNSaveManager.CaptureJson(runner),
            };
            SaveSystem.SaveToSlot(SaveSystem.AutoSlot, pd);
        });
        
        runner.AddCommandHandler("vn_end", async () => { await SceneFlow.LoadFrontEndAsync(); });
    }
    
    
    static float ParseF(string s, float d=0f) => float.TryParse(s, out var v) ? v : d;
    static bool TryParseFloat(string s, out float v) => float.TryParse(s, out v);
    static bool  ParseB(string s) => bool.TryParse(s, out var v) && v;
    // --- Commands ---
    
    IEnumerator BgVarArgs(string[] a) {
        if (!stage || a == null || a.Length == 0) yield break;
        string id = a[0]; string variant = null; float dur = 0.4f;
        int colon = id.IndexOf(':'); if (colon >= 0) { variant = id[(colon+1)..]; id = id[..colon]; }
        if (a.Length >= 2) { if (float.TryParse(a[1], out var d)) dur = d; else variant = a[1]; }
        if (a.Length >= 3) { if (float.TryParse(a[2], out var d)) dur = d; else variant = a[2]; }
        yield return stage.CoBG(id, dur, variant);
    }

    IEnumerator ShowVarArgs(string[] a) {
        if (!stage || a == null || a.Length < 3) yield break;
        string id = a[0], expr = a[1], slot = a[2];
        float dur = (a.Length >= 4 && float.TryParse(a[3], out var d)) ? d : 0.35f;
        bool flip = (a.Length >= 5 && bool.TryParse(a[4], out var b)) ? b : false;
        yield return stage.CoShow(id, expr, slot, dur, flip);
    }

    IEnumerator HideVarArgs(string[] a) {
        if (!stage || a == null || a.Length < 1) yield break;
        string id = a[0];
        float dur = (a.Length >= 2 && float.TryParse(a[1], out var d)) ? d : 0.25f;
        yield return stage.CoHide(id, dur);
    }

    IEnumerator MoveVarArgs(string[] a) {
        if (!stage || a == null || a.Length < 2) yield break;
        string id = a[0], slot = a[1];
        float dur = (a.Length >= 3 && float.TryParse(a[2], out var d)) ? d : 0.35f;
        yield return stage.CoMove(id, slot, dur);
    }

    IEnumerator ExprVarArgs(string[] a) {
        if (!stage || a == null || a.Length < 2) yield break;
        string id = a[0], expr = a[1];
        float dur = (a.Length >= 3 && float.TryParse(a[2], out var d)) ? d : 0.2f;
        yield return stage.CoExpr(id, expr, dur);
    }

    IEnumerator FlipVarArgs(string[] a) {
        if (!stage || a == null || a.Length < 1) yield break;
        string id = a[0]; bool flip = (a.Length >= 2 && bool.TryParse(a[1], out var b)) ? b : true;
        yield return stage.CoFlip(id, flip);
    }

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
