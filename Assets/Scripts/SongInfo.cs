using UnityEngine;

[CreateAssetMenu(menuName = "Songs/Song Info")]
public class SongInfo : ScriptableObject
{
    [Header("Identity")]
    public string title;
    public string artist;
    public Sprite jacket;

    [Header("Audio / Timing")]
    public AudioClip previewClip;
    [Tooltip("Seconds into the clip to start the preview.")] public float previewStart = 0f;
    [Tooltip("Loop back here if previewEnd <= 0.")] public float previewLoop = 0f;
    [Tooltip("0 = loop to previewLoop. >0 = stop preview here.")] public float previewEnd = 0f;
    public float bpm = 120f;

    [Header("DDR Radar (0..1)")]
    [Range(0,1)] public float stream;
    [Range(0,1)] public float voltage;
    [Range(0,1)] public float freeze;
    [Range(0,1)] public float chaos;
    [Range(0,1)] public float air;   // ok to keep even if you don’t show jumps yet
}