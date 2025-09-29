// BackgroundDef.cs
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Video;

[CreateAssetMenu(menuName="VN/Background", fileName="BG_New")]
public class BackgroundDef : ScriptableObject
{
    [Tooltip("Stable key used in Yarn, e.g. 'classroom'")]
    public string id;

    [Header("Primary assets")]
    public Sprite sprite;         // for static BGs
    public VideoClip video;       // optional: video BGs

   
    [Serializable] public struct Variant {
        public string key;        // e.g. "night", "rain", "jp"
        public Sprite sprite;
        public VideoClip video;
    }
    [Header("Optional variants (time of day, locale, etc.)")]
    public List<Variant> variants = new();

    public (Sprite sprite, VideoClip video) Resolve(string variantKey=null) {
        if (!string.IsNullOrEmpty(variantKey)) {
            foreach (var v in variants)
                if (string.Equals(v.key, variantKey, StringComparison.OrdinalIgnoreCase))
                    return (v.sprite ? v.sprite : sprite, v.video ? v.video : video);
        }
        return (sprite, video);
    }
}