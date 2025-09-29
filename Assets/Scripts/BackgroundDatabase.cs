// BackgroundDatabase.cs
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName="VN/Background Database", fileName="BGDatabase")]
public class BackgroundDatabase : ScriptableObject
{
    public List<BackgroundDef> entries = new();
    Dictionary<string, BackgroundDef> _map;

    void OnEnable() {
        _map = new Dictionary<string, BackgroundDef>(System.StringComparer.OrdinalIgnoreCase);
        foreach (var e in entries) if (e && !string.IsNullOrEmpty(e.id)) _map[e.id] = e;
    }

    public bool TryGet(string id, out BackgroundDef def) {
        if (_map == null) OnEnable();
        return _map.TryGetValue(id, out def);
    }
}