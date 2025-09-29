using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "UI/Credits Data")]
public class CreditsData : ScriptableObject
{
    [Header("Top Title")]
    public string title = "Credits";

    [Header("Legacy (still supported)")]
    [Tooltip("If you only fill this, CreditsScreen will build a simple list of names.")]
    public List<string> names = new();

    [Header("Rich Credits (optional)")]
    public List<CreditSection> sections = new();

    [Header("Software Licenses (optional)")]
    [Tooltip("Plain text assets appended at the end (e.g., 3rd-party licenses).")]
    public List<TextAsset> licenseFiles = new();
}

[Serializable]
public class CreditSection
{
    public string heading;
    public List<CreditItem> items = new();
}

[Serializable]
public class CreditItem
{
    public enum Kind
    {
        Name,             // uses 'name'
        NameWithRole,     // uses 'name' + 'role'
        Paragraph,        // uses 'paragraph'
        Logo,             // uses 'logo', 'logoHeight'
        Spacer,           // uses 'spaceHeight'
    }

    public Kind kind = Kind.Name;

    [Header("Name / Role")]
    public string name;
    public string role;

    [Header("Paragraph")]
    [TextArea(3, 10)]
    public string paragraph;

    [Header("Logo")]
    public Sprite logo;
    public float logoHeight = 128f;

    [Header("Spacer")]
    public float spaceHeight = 32f;
}