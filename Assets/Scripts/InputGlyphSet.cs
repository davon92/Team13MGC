using UnityEngine;

[CreateAssetMenu(menuName="Input/Glyph Set")]
public class InputGlyphSet : ScriptableObject
{
    [Header("Face buttons")]
    public Sprite faceSouth;   // A / Cross / Q
    public Sprite faceEast;    // B / Circle / W
    public Sprite faceWest;    // X / Square / E
    public Sprite faceNorth;   // Y / Triangle / R

    [Header("Other")]
    public Sprite start;
    public Sprite back;
    
    [Header("UI buttons")]
    public Sprite uiSelect;
    public Sprite uiChange;
    public Sprite uiSubmit;
    public Sprite uiCancel;
    public Sprite uiFullNav;
    
    [Header("Knob Icon")]
    [Tooltip("Shown for the knob lane: Left Stick (gamepad) or Mouse (kbm).")]
    public Sprite knobIcon;
}