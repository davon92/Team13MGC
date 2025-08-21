using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class SceneFlow
{
    public const string FrontEndSceneName = "FrontEndScene";
    public const string VNSceneName       = "VNGamePlayScene";
    public const string RhythmSceneName   = "RhythmGamePlayScene";

    // Note: we keep the existing signature so callers don't break.
    // newGame / chapterId are intentionally ignored here; VNBootstrap decides.
    public static async Task LoadVNAsync(bool newGame = false, string chapterId = null, float fadeDuration = 0.35f)
    {
        if (Fade.Instance != null) await Fade.Instance.Out(fadeDuration);

        var op = SceneManager.LoadSceneAsync(VNSceneName, LoadSceneMode.Single);
        while (!op.isDone) await Task.Yield();

        if (Fade.Instance != null) await Fade.Instance.In(fadeDuration);
    }

    public static async Task BackToFrontEndAsync(float fadeDuration = 0.35f)
    {
        if (Fade.Instance != null) await Fade.Instance.Out(fadeDuration);

        var op = SceneManager.LoadSceneAsync(FrontEndSceneName, LoadSceneMode.Single);
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