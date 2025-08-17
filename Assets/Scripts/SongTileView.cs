using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using DG.Tweening;

/// <summary>
/// Visual for one song row. We NEVER scale the layout root, only an inner
/// "Content" wrapper whose pivot is forced to (1, 0.5) so width expansion
/// grows LEFT from the right edge (IIDX feel) without fighting the layout.
/// </summary>
public class SongTileView : MonoBehaviour
{
    [Header("Refs (optional)")]
    [SerializeField] Image    bg;
    [SerializeField] Image    jacket;
    [SerializeField] TMP_Text title;
    [SerializeField] TMP_Text artist;
    [SerializeField] Button   button;

    [Header("Focus Look")]
    [Tooltip("Target X scale when this tile is under the selector.")]
    [SerializeField, Range(1f, 1.6f)] float focusedScaleX = 1.10f;
    [SerializeField, Range(0f, 0.5f)] float tween = 0.12f;
    [SerializeField] Ease ease = Ease.OutCubic;

    // We never touch the layout root (this RectTransform).
    RectTransform layoutRoot;

    // This wrapper is what we animate. If missing, we create/migrate into it.
    [SerializeField] RectTransform content;

    Tween scaleTween;

    public Button Button => button;

    void Awake()
    {
        layoutRoot = (RectTransform)transform;
        EnsureContentWrapper();
    }

    void OnDisable()
    {
        scaleTween?.Kill();
        scaleTween = null;
    }

    /// <summary>Make sure we have a Content wrapper with the right pivot/anchors.</summary>
    void EnsureContentWrapper()
    {
        if (content == null)
        {
            var found = transform.Find("Content") as RectTransform;
            if (found == null)
            {
                var go = new GameObject("Content", typeof(RectTransform));
                content = go.GetComponent<RectTransform>();
                content.SetParent(layoutRoot, false);

                // Fill the cell
                content.anchorMin = Vector2.zero;
                content.anchorMax = Vector2.one;
                content.offsetMin = Vector2.zero;
                content.offsetMax = Vector2.zero;
                content.pivot     = new Vector2(1f, 0.5f); // grow from right

                // Move any existing visuals under Content so we scale them together
                var toMove = new List<Transform>();
                foreach (Transform c in layoutRoot)
                    if (c != content) toMove.Add(c);
                foreach (var t in toMove)
                    t.SetParent(content, true);
            }
            else
            {
                content = found;
            }
        }

        // Enforce correct shape every time (prefab safety)
        content.anchorMin = Vector2.zero;
        content.anchorMax = Vector2.one;
        content.offsetMin = Vector2.zero;
        content.offsetMax = Vector2.zero;
        content.pivot     = new Vector2(1f, 0.5f);

        // sane starting scale
        content.localScale = new Vector3(1f, 1f, 1f);
    }

    /// <summary>Called by SongSelectScreen after a move completes.</summary>
    public void SetFocused(bool on, bool instant = false)
    {
        EnsureContentWrapper();

        float targetX = on ? focusedScaleX : 1f;

        scaleTween?.Kill();
        if (instant || tween <= 0f)
        {
            content.localScale = new Vector3(targetX, 1f, 1f);
        }
        else
        {
            scaleTween = content
                .DOScaleX(targetX, tween)
                .SetEase(ease)
                .SetUpdate(true);
        }
    }

    /// <summary>
    /// Optional continuous weighting (0..1) used while the list is sliding.
    /// You can keep this even if you prefer the simpler one-tile focus.
    /// </summary>
    public void SetFocusWeight(float w)
    {
        EnsureContentWrapper();
        float targetX = Mathf.Lerp(1f, focusedScaleX, Mathf.Clamp01(w));
        content.localScale = new Vector3(targetX, 1f, 1f);
    }

    // Binder the screen calls when a tile shows a new song
    public void Bind(SongInfo s)
    {
        if (jacket) { jacket.sprite = s.jacket; jacket.enabled = s.jacket != null; }
        if (title)  title.text  = s.title;
        if (artist) artist.text = s.artist;
    }
}
