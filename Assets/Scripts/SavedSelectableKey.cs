// SavedSelectableKey.cs
using UnityEngine;

[DisallowMultipleComponent]
public class SavedSelectableKey : MonoBehaviour
{
    [SerializeField] private string key;
    public string Key => key;

#if UNITY_EDITOR
    // Helps ensure keys are never blank in editor
    private void OnValidate()
    {
        if (string.IsNullOrWhiteSpace(key))
            key = gameObject.name; // fallback, but prefer a stable string or GUID
    }
#endif
}