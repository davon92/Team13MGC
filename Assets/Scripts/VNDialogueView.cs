using UnityEngine;
using TMPro;

/// <summary>
/// Temporary, API-safe UI helper for your VN HUD.
/// This is NOT a Yarn presenter (on purpose) so it won’t clash with Yarn 3.x changes.
/// Leave Yarn’s display/flow to the built-in presenters (LineView, OptionsListView).
///
/// Use this class to:
///  - Show a custom "Continue" hint
///  - Toggle skip/auto indicators
///  - Update speaker name/title text you drive via Yarn commands (<<set>>/custom commands)
///
/// When you're ready to fully customize text/choices rendering, we’ll implement a true
/// DialoguePresenterBase later. For now, this avoids the legacy API errors and compiles cleanly.
/// </summary>
public class VNDialogueView : MonoBehaviour
{
    [Header("UI")]
    [SerializeField] private CanvasGroup root;        // whole VN HUD group
    [SerializeField] private TextMeshProUGUI nameText;
    [SerializeField] private GameObject continueHint; // e.g., "Press A / Click to continue"
    [SerializeField] private GameObject autoIcon;     // optional
    [SerializeField] private GameObject skipIcon;     // optional

    private void Reset()
    {
        root = GetComponent<CanvasGroup>();
    }

    public void Show(bool visible, float alpha = 1f)
    {
        if (root == null)
            return;

        root.alpha = visible ? alpha : 0f;
        root.interactable = visible;
        root.blocksRaycasts = visible;
    }

    public void SetSpeakerName(string displayName)
    {
        if (nameText != null)
            nameText.text = displayName ?? string.Empty;
    }

    public void ShowContinueHint(bool visible)
    {
        if (continueHint != null)
            continueHint.SetActive(visible);
    }

    public void ShowAuto(bool visible)
    {
        if (autoIcon != null)
            autoIcon.SetActive(visible);
    }

    public void ShowSkip(bool visible)
    {
        if (skipIcon != null)
            skipIcon.SetActive(visible);
    }
}