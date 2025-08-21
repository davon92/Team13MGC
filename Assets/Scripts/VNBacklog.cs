using System.Collections.Generic;
using UnityEngine;

/// Barebones backlog – stores final shown lines, in order.
/// Add your own UI later to scroll and display these.
public class VNBacklog : MonoBehaviour
{
    [SerializeField] int maxEntries = 200;

    readonly List<string> lines = new();

    public void Add(string line)
    {
        if (string.IsNullOrWhiteSpace(line)) return;
        lines.Add(line);
        if (lines.Count > maxEntries) lines.RemoveAt(0);
        // Debug.Log($"[Backlog] {line}");
    }

    public IReadOnlyList<string> Lines => lines;
}