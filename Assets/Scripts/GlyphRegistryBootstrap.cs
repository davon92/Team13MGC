// GlyphRegistryBootstrap.cs (new)
using UnityEngine;

public class GlyphRegistryBootstrap : MonoBehaviour {
    public InputGlyphSet xbox, playStation, nintendo, keyboardMouse;
    void Awake() {
        GlyphLibrary.Init(xbox, playStation, nintendo, keyboardMouse);
        DontDestroyOnLoad(gameObject);
    }
}