using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;
using System.Collections;
using UnityEngine.InputSystem;

public class ResultsScreen : MonoBehaviour, IUIScreen
{
    [Header("Wiring")]
    [SerializeField] private GameObject root;            // Screen_Results panel
    [SerializeField] private GameObject firstSelected;   // Back button (or Continue)
    [SerializeField] private ScreenController screens;   // optional; used only for navigation
    [SerializeField] private Button backButton;
    
    [Header("Meta")]
    [SerializeField] private Image    coverImage;
    [SerializeField] private TMP_Text titleText;
    [SerializeField] private TMP_Text artistText;
    [SerializeField] private TMP_Text gradeText;

    [Header("Score")]
    [SerializeField] private TMP_Text scoreText;     // big rolling number
    [SerializeField] private TMP_Text outOfText;     // "/ 10,000,000"

    [Header("Tallies")]
    [SerializeField] private TMP_Text maxComboVal;
    [SerializeField] private TMP_Text perfectVal;
    [SerializeField] private TMP_Text greatVal;
    [SerializeField] private TMP_Text goodVal;
    [SerializeField] private TMP_Text missVal;

    [Header("Striping (optional)")]
    [SerializeField] private Image[] stripeRows;     // row backgrounds: MaxCombo, Perfect, Great, Good, Miss
    [SerializeField] private Color stripeA = new Color(0.75f, 0.75f, 0.75f, 1f);
    [SerializeField] private Color stripeB = new Color(0.58f, 0.58f, 0.58f, 1f);

    [Header("Grade thresholds (by score %)")]
    [Range(0f,1f)] public float AAA = 0.990f;
    [Range(0f,1f)] public float AA  = 0.970f;
    [Range(0f,1f)] public float A   = 0.950f;
    [Range(0f,1f)] public float B   = 0.900f;
    [Range(0f,1f)] public float C   = 0.800f;
    [Range(0f,1f)] public float D   = 0.700f;

    [Header("Roll settings")]
    [SerializeField] private float rollSeconds = 0.9f;
    [SerializeField] private AnimationCurve rollCurve = AnimationCurve.EaseInOut(0,0,1,1);

    public string     ScreenId      => MenuIds.Results;
    public GameObject Root          => root != null ? root : gameObject;
    public GameObject FirstSelected => firstSelected;
    [SerializeField] string songSelectScreenId = MenuIds.SongSelect;
    void Awake()
    {
        if (root == null) root = gameObject;

        // alternating rows
        if (stripeRows != null)
            for (int i = 0; i < stripeRows.Length; i++)
                if (stripeRows[i]) stripeRows[i].color = (i % 2 == 0) ? stripeA : stripeB;
        
        if (backButton != null)
            backButton.onClick.AddListener(OnBack);
    }

    public void OnShow(object args)
    {
        // Try to consume the fresh result; otherwise fall back to last.
        if (!SceneFlow.TryConsumePendingRhythmResult(out var rr))
            rr = SceneFlow.LastRhythmResult;

        if (rr == null) { Debug.LogWarning("ResultsScreen: no RhythmResult available."); return; }

        // Cover / Title / Artist
        if (rr.song)
        {
            if (coverImage) coverImage.sprite = rr.song.jacket;   // your SongInfo sprite
            if (titleText)  titleText.text    = string.IsNullOrEmpty(rr.song.title) ? rr.song.name : rr.song.title;
            if (artistText) artistText.text   = rr.song.artist ?? "";
        }

        // Tallies
        if (maxComboVal) maxComboVal.text = rr.maxCombo.ToString("N0");
        if (perfectVal)  perfectVal.text  = rr.perfect.ToString("N0");
        if (greatVal)    greatVal.text    = rr.great.ToString("N0");
        if (goodVal)     goodVal.text     = rr.good.ToString("N0");
        if (missVal)     missVal.text     = rr.miss.ToString("N0");

        // Grade
        var grade = EvaluateGrade(rr.score, 10_000_000);
        rr.grade = grade;
        if (gradeText) gradeText.text = grade;

        // Score labels
        if (outOfText) outOfText.text = "/ 10,000,000";
        StopAllCoroutines();
        StartCoroutine(RollScore(0, rr.score));

        if (FirstSelected && EventSystem.current)
            EventSystem.current.SetSelectedGameObject(FirstSelected);
    }

    public void OnHide() { /* nothing needed */ }

    string EvaluateGrade(int score, int max)
    {
        float p = max > 0 ? (float)score / max : 0f;
        if (p >= AAA) return "AAA";
        if (p >= AA)  return "AA";
        if (p >= A)   return "A";
        if (p >= B)   return "B";
        if (p >= C)   return "C";
        if (p >= D)   return "D";
        return "E";
    }

    IEnumerator RollScore(int from, int to)
    {
        if (!scoreText) yield break;
        float t = 0f;
        while (t < 1f)
        {
            t += Time.unscaledDeltaTime / Mathf.Max(0.01f, rollSeconds);
            float k = rollCurve.Evaluate(Mathf.Clamp01(t));
            int v = Mathf.RoundToInt(Mathf.Lerp(from, to, k));
            scoreText.text = v.ToString("N0"); // commas
            yield return null;
        }
        scoreText.text = to.ToString("N0");
    }
    
    private void OnBack()
    {
        OnBackToSongSelect();
    }
    
    public void OnUI_Cancel(InputValue value) => OnBack();
    // UI hook for your Back button
    public void OnBackToSongSelect()
    {
        // Works whether or not you added the LoadFrontEndAsync(startScreenId) overload.
        SceneFlow.SetFrontEndStartup(songSelectScreenId);
        _ = SceneFlow.LoadFrontEndAsync();   // FrontEndBootstrap will jump straight to Song Select
    }
}
