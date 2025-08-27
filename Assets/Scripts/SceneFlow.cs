using System;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;

/// Centralized scene/router with a tiny "travel ticket" for the rhythm game.
public static class SceneFlow
{
    public const string FrontEndSceneName = "FrontEndScene";
    public const string VNSceneName       = "VNGamePlayScene";
    public const string RhythmSceneName   = "RhythmGamePlayScene";

    // ---- Rhythm "travel ticket" ----
    public enum RhythmOrigin { SongSelect, VisualNovel }
    

    [System.Serializable] public sealed class RhythmRequest {
        public SongInfo song;
        public RhythmOrigin origin;
        public string returnYarnNode;   // VN node to resume (optional)
        public int difficulty;
        public override string ToString() =>
            $"RhythmRequest(song={(song ? song.name : "null")}, origin={origin}, return={returnYarnNode})";
    }

    [System.Serializable]
    public sealed class RhythmResult {
        public SongInfo song;
        public RhythmOrigin origin;

        public int    score;
        public bool   cleared;     // you can set this to true/false based on a rule you like
        public int    maxCombo;
        public int    perfect;
        public int    great;
        public int    good;
        public int    miss;
        public string grade;       // "AAA", "AA", "A", etc.
    }
    
    public static RhythmRequest PendingRhythm { get; private set; }
    public static RhythmResult PendingRhythmResult { get; private set; }
    public static RhythmResult LastRhythmResult    { get; private set; }

    public static string PendingVNStartNode { get; set; }

    // ---- Entry points ----
    public static async Task GoToRhythmFromSongSelectAsync(SongInfo song, int difficulty = 0, float fade = 0.35f)
    {
        PendingRhythm = new RhythmRequest { song = song, origin = RhythmOrigin.SongSelect, difficulty = difficulty };
        await LoadRhythmAsync(fade);
    }

    public static async Task GoToRhythmFromVNAsync(SongInfo song, string returnYarnNode, int difficulty = 0, float fade = 0.35f)
    {
        PendingRhythm = new RhythmRequest { song = song, origin = RhythmOrigin.VisualNovel, returnYarnNode = returnYarnNode, difficulty = difficulty };
        await LoadRhythmAsync(fade);
    }

    /// Called by the rhythm scene when the song ends.
    // SceneFlow.cs
    public static async System.Threading.Tasks.Task ReturnFromRhythmAsync(
        RhythmResult result,
        bool alreadyFaded = false,
        float fadeOut = 0.35f,
        float fadeIn  = 0.25f)
    {
        // put result in both places
        PendingRhythmResult = result;
        LastRhythmResult    = result;

        var req = PendingRhythm;      // consume the “ticket”
        PendingRhythm = null;

        if (!alreadyFaded && Fade.Instance != null)
            await Fade.Instance.Out(fadeOut);

        if (req != null && req.origin == RhythmOrigin.SongSelect)
        {
            var op = UnityEngine.SceneManagement.SceneManager
                .LoadSceneAsync(FrontEndSceneName, UnityEngine.SceneManagement.LoadSceneMode.Single);
            while (!op.isDone) await System.Threading.Tasks.Task.Yield();
        }
        else
        {
            var op = UnityEngine.SceneManagement.SceneManager
                .LoadSceneAsync(VNSceneName, UnityEngine.SceneManagement.LoadSceneMode.Single);
            while (!op.isDone) await System.Threading.Tasks.Task.Yield();

            if (!string.IsNullOrWhiteSpace(req?.returnYarnNode) && VNController.Instance != null)
                VNController.Instance.StartConversation(req.returnYarnNode);
        }

        if (Fade.Instance != null)
            await Fade.Instance.In(fadeIn);
    }


    public static void SubmitRhythmResult(RhythmResult r)
    {
        PendingRhythmResult = r;
        LastRhythmResult    = r;
    }
    
    public static bool TryConsumePendingRhythmResult(out RhythmResult r)
    {
        r = PendingRhythmResult;
        if (r == null) return false;
        PendingRhythmResult = null;
        return true;
    }

    // ---- Basic loaders (fade-aware) ----
    public static async Task LoadFrontEndAsync(float fadeDuration = 0.35f)
    {
        if (Fade.Instance != null) await Fade.Instance.Out(fadeDuration);
        var op = SceneManager.LoadSceneAsync(FrontEndSceneName, LoadSceneMode.Single);
        while (!op.isDone) await Task.Yield();
        if (Fade.Instance != null) await Fade.Instance.In(fadeDuration);
    }

    // newGame/chapterId are ignored; VNBootstrap decides.
    public static async Task LoadVNAsync(bool newGame = false, string chapterId = null, float fadeDuration = 0.35f)
    {
        if (Fade.Instance != null) await Fade.Instance.Out(fadeDuration);
        var op = SceneManager.LoadSceneAsync(VNSceneName, LoadSceneMode.Single);
        while (!op.isDone) await Task.Yield();
        if (Fade.Instance != null) await Fade.Instance.In(fadeDuration);
    }

    public static async Task LoadRhythmAsync(float fadeDuration = 0.35f)
    {
        if (Fade.Instance != null) await Fade.Instance.Out(fadeDuration);
        var op = SceneManager.LoadSceneAsync(RhythmSceneName, LoadSceneMode.Single);
        while (!op.isDone) await Task.Yield();
        if (Fade.Instance != null) await Fade.Instance.In(fadeDuration);
    }
}
