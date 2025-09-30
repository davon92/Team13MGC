// VNCo.cs
using UnityEngine;
using System.Collections;

public static class VNCo
{
    class Runner : MonoBehaviour { }
    static Runner _runner;

    static Runner GetRunner() {
        if (_runner) return _runner;
        var go = new GameObject("~VNCo");
        Object.DontDestroyOnLoad(go);
        _runner = go.AddComponent<Runner>();
        return _runner;
    }

    public static void Start(IEnumerator routine) => GetRunner().StartCoroutine(routine);
}