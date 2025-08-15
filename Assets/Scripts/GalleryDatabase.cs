// Assets/Scripts/Gallery/GalleryDatabase.cs
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "Game/Gallery Database")]
public class GalleryDatabase : ScriptableObject
{
    public List<GalleryItem> items = new List<GalleryItem>();
}