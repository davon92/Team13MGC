using UnityEngine;
using SonicBloom.Koreo;   // Koreography

[CreateAssetMenu(menuName = "Songs/Song Info")]
public class SongInfo : ScriptableObject
{
    [Header("Identity")]
    public string title;
    public string artist;
    public Sprite jacket;
    [Header("Identity")]
    [SerializeField] private string id;
    public string SongId => string.IsNullOrEmpty(id) ? name : id;
    
#if UNITY_EDITOR
    void OnValidate() {
        // Default the id to the asset name if it’s empty.
        if (string.IsNullOrEmpty(id)) id = name;
    }
#endif
    
    [Header("Audio / Chart")]
    [Tooltip("Koreographer chart that defines all timing/event tracks.")]
    public Koreography koreography;

    [Tooltip("Music clip to play. If empty, uses koreography.SourceClip.")]
    public AudioClip musicClip;

    [Tooltip("Koreography Track ID for face buttons (int payload 0..3).")]
    public string buttonsEventID = "Buttons";

    [Tooltip("Koreography Track ID for knob (curve/float payload).")]
    public string knobEventID = "Knob";

    [Header("Preview (Song Select)")]
    public AudioClip previewClip;
    [Tooltip("Seconds into the clip to start the preview.")] public float previewStart = 0f;
    [Tooltip("Loop back here if previewEnd <= 0.")] public float previewLoop = 0f;
    [Tooltip("0 = loop to previewLoop. >0 = stop preview here.")] public float previewEnd = 0f;

    [Header("Timing Metadata")]
    public float bpm = 120f;

    [Header("DDR Radar (0..1)")]
    [Range(0,1)] public float stream;
    [Range(0,1)] public float voltage;
    [Range(0,1)] public float freeze;
    [Range(0,1)] public float chaos;
    [Range(0,1)] public float air;

    public AudioClip Music => musicClip ? musicClip : koreography != null ? koreography.SourceClip : null;
}