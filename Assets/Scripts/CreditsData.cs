using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "UI/Credits Data")]
public class CreditsData : ScriptableObject
{
    public string title = "Credits";
    public List<string> names = new();
}