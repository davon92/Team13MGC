// NoteStyleSet.cs
using UnityEngine;
using static RhythmTypes;

[CreateAssetMenu(fileName="NoteStyleSet", menuName="Rhythm/Note Style Set")]
public class NoteStyleSet : ScriptableObject
{
    [Header("Sprites")]
    public Sprite iconA;
    public Sprite iconY;
    public Sprite iconB;
    public Sprite iconX;

    [Header("Optional tint per button")]
    public Color  colorA = Color.white;
    public Color  colorY = Color.white;
    public Color  colorB = Color.white;
    public Color  colorX = Color.white;

    public Sprite GetSprite(FaceButton b) => b switch {
        FaceButton.A => iconA,
        FaceButton.Y => iconY,
        FaceButton.B => iconB,
        FaceButton.X => iconX,
        _ => iconA
    };

    public Color GetColor(FaceButton b) => b switch {
        FaceButton.A => colorA,
        FaceButton.Y => colorY,
        FaceButton.B => colorB,
        FaceButton.X => colorX,
        _ => Color.white
    };
}