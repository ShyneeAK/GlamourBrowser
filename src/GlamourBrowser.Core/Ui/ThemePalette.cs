namespace GlamourBrowser.Core.Ui;

/// <summary>All colours of the plugin's theme, ImGui ABGR.</summary>
public readonly record struct ThemeColors(
    uint WindowBg,
    uint Card,
    uint CardRaised,
    uint Border,
    uint Text,
    uint TextMuted,
    uint Accent,
    uint AccentHover,
    uint AccentMuted,
    uint Owned,
    uint Missing,
    uint Warning);

/// <summary>Modern dark palette with one selectable accent; green stays reserved for owned items.</summary>
public static class ThemePalette
{
    private const uint WindowBg = 0xFF1D1816;
    private const uint Card = 0xFF27201D;
    private const uint CardRaised = 0xFF2E2622;
    private const uint Border = 0xFF3D332E;
    private const uint Text = 0xFFEDE7E3;
    private const uint TextMuted = 0xFFA3998B;
    private const uint Owned = Rendering.TileStyle.OwnedColor;
    private const uint Missing = Rendering.TileStyle.MissingColor;
    private const uint Warning = 0xFF4AC0F0;
    private const uint White = 0xFFFFFFFF;

    public static ThemeColors For(AccentColor accent)
    {
        var a = AccentValue(accent);
        return new ThemeColors(WindowBg, Card, CardRaised, Border, Text, TextMuted,
            a, Mix(a, White, 0.15f), Mix(a, Card, 0.7f), Owned, Missing, Warning);
    }

    /// <summary>Linear mix per channel; the result is always opaque.</summary>
    public static uint Mix(uint a, uint b, float t)
    {
        uint Channel(int shift)
        {
            var ca = (a >> shift) & 0xFF;
            var cb = (b >> shift) & 0xFF;
            return (uint)Math.Clamp(MathF.Round(ca + (cb - (float)ca) * t), 0, 255) << shift;
        }

        return 0xFF000000 | Channel(16) | Channel(8) | Channel(0);
    }

    /// <summary>Relative luminance (Rec. 709 weights on the raw channels), 0..1.</summary>
    public static float Luminance(uint abgr)
    {
        var r = (abgr & 0xFF) / 255f;
        var g = ((abgr >> 8) & 0xFF) / 255f;
        var b = ((abgr >> 16) & 0xFF) / 255f;
        return 0.2126f * r + 0.7152f * g + 0.0722f * b;
    }

    // RGB in comments, ABGR in code.
    private static uint AccentValue(AccentColor accent) => accent switch
    {
        AccentColor.Violet => 0xFFFF70A9, // #A970FF
        AccentColor.Teal => 0xFFA3B32B,   // #2BB3A3
        AccentColor.Rose => 0xFF8F6DE2,   // #E26D8F
        AccentColor.Amber => 0xFF3AA4E0,  // #E0A43A
        _ => 0xFFEF8D5B,                  // #5B8DEF
    };
}
