// GlyphLibrary.cs
using UnityEngine;

public static class GlyphLibrary
{
    static InputGlyphSet xbox, ps, nin, kbm;

    public static void Init(InputGlyphSet x, InputGlyphSet p, InputGlyphSet n, InputGlyphSet k)
    {
        xbox = x; ps = p; nin = n; kbm = k;

        // ✅ Ask SettingsService to raise the current glyph style event
        SettingsService.ApplyGameplay();
    }

    public static InputGlyphSet Get(InputGlyphStyle s) => s switch
    {
        InputGlyphStyle.Xbox        => xbox,
        InputGlyphStyle.PlayStation => ps,
        InputGlyphStyle.Nintendo    => nin ?? xbox,
        _                           => kbm,
    };

    public static InputGlyphSet Current => Get(SettingsService.EffectiveGlyphStyle);
}