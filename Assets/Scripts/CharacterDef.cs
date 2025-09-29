// CharacterDef.cs
using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName="VN/Character", fileName="NewCharacter")]
public class CharacterDef : ScriptableObject {
    public string id;                          // "ayaka" (must be unique)
    public Sprite defaultExpression;           // fallback
    [Serializable] public struct Expr {
        public string name;                    // "happy"
        public Sprite sprite;
    }
    public List<Expr> expressions = new();
    public Sprite Get(string exprName) {
        if (string.IsNullOrEmpty(exprName)) return defaultExpression;
        foreach (var e in expressions) if (string.Equals(e.name, exprName, StringComparison.OrdinalIgnoreCase)) return e.sprite ?? defaultExpression;
        return defaultExpression;
    }
}