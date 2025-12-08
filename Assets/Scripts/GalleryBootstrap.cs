using UnityEngine;

public class GalleryBootstrap : MonoBehaviour
{
    [SerializeField] GalleryDatabase database;

    void Awake()
    {
        // Seeds default unlocks only if no prior gallery save exists.
        GallerySaves.EnsureDefaults(database);
    }
}

