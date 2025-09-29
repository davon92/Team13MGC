using System;
using System.Collections.Generic;
using UnityEngine;
using Yarn.Unity;

[Serializable]
public class VNState {
    public string node;
    public Vars vars = new Vars();
    [Serializable] public class Vars {
        public List<string> names = new();
        public List<string> types = new();  // "string" | "number" | "bool"
        public List<string> sVals = new();
        public List<float>  fVals = new();
        public List<bool>   bVals = new();
    }
}

public static class VNSaveManager
{
    public static string LastNode { get; set; } = "Prologue";

    // add/remove as your VN grows
    static readonly string[] WatchedVars = { "$lastGrade", "$lastScore", "$cleared", "$maxCombo", "$songId" };

    public static string CaptureJson(DialogueRunner runner) =>
        JsonUtility.ToJson(Capture(runner));

    public static VNState Capture(DialogueRunner runner)
    {
        var s = new VNState { node = LastNode };
        var vs = runner?.VariableStorage;
        if (vs != null) {
            foreach (var name in WatchedVars) {
                if (vs.TryGetValue(name, out string sv)) { s.vars.names.Add(name); s.vars.types.Add("string"); s.vars.sVals.Add(sv); continue; }
                if (vs.TryGetValue(name, out float  fv)) { s.vars.names.Add(name); s.vars.types.Add("number"); s.vars.fVals.Add(fv); continue; }
                if (vs.TryGetValue(name, out bool   bv)) { s.vars.names.Add(name); s.vars.types.Add("bool");   s.vars.bVals.Add(bv); continue; }
            }
        }
        return s;
    }

    public static bool TryApplyJson(DialogueRunner runner, string json, out string resumeNode)
    {
        resumeNode = null;
        if (runner == null || string.IsNullOrEmpty(json)) return false;

        var state = JsonUtility.FromJson<VNState>(json);
        if (state == null) return false;

        var vs = runner.VariableStorage;
        int si = 0, fi = 0, bi = 0;
        for (int i = 0; i < state.vars.names.Count; i++) {
            var n = state.vars.names[i];
            switch (state.vars.types[i]) {
                case "string": vs.SetValue(n, state.vars.sVals[si++]); break;
                case "number": vs.SetValue(n, state.vars.fVals[fi++]); break;
                case "bool":   vs.SetValue(n, state.vars.bVals[bi++]); break;
            }
        }
        resumeNode = string.IsNullOrEmpty(state.node) ? "Prologue" : state.node;
        return true;
    }
}
