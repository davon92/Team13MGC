// Assets/Scripts/Gallery/GalleryItem.cs
using UnityEngine;

[CreateAssetMenu(menuName = "Game/Gallery Item")]
public class GalleryItem : ScriptableObject
{
    [Header("Identity")]
    public string id;          // unique key, e.g. "art_stage01_cg1"
    public string title;

    [Header("Sprites")]
    public Sprite thumbnail;   // small square
    public Sprite fullImage;   // full-res (UI/Image will scale to fit)

    [TextArea] public string caption;   // optional

    [Header("Behavior")]
    public bool hideIfLocked = false;   // otherwise show as locked tile
}