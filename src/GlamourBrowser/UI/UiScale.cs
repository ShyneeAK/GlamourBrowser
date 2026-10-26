using System;
using Dalamud.Interface;
using Dalamud.Interface.ManagedFontAtlas;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;

namespace GlamourBrowser.UI;

/// <summary>
/// The plugin's own UI scale on top of Dalamud's: text and FontAwesome icons come from fonts built at the scaled
/// size (sharp, not stretched), fixed pixel sizes are multiplied through <see cref="Global"/>. At 100 % the default fonts are used.
/// </summary>
public static class UiScale
{
    public const float Min = 0.5f;
    public const float Max = 3f;

    private static IFontHandle? text;
    private static IFontHandle? icons;
    private static IFontHandle? large;
    private static float largeFactor;
    // Text that has to stand out next to large icons (the row under the slot bar).
    private const float LargeText = 1.25f;

    public static float Factor { get; private set; } = 1f;

    /// <summary>The text size on top of <see cref="Factor"/> (the "Schriftgröße" setting).</summary>
    public static float FontFactor { get; private set; } = 1f;

    /// <summary>Dalamud's global scale times the plugin's factor, for fixed pixel sizes.</summary>
    public static float Global => ImGuiHelpers.GlobalScale * Factor;

    /// <summary>
    /// Rebuilds the fonts for a new UI scale and text size. Not while one of them is pushed: disposing a pushed font
    /// handle throws.
    /// </summary>
    public static void Set(float factor, float fontFactor)
    {
        factor = Math.Clamp(factor, Min, Max);
        fontFactor = Math.Clamp(fontFactor, 0.8f, 2f);
        var unchanged = Math.Abs(factor - Factor) < 0.001f && Math.Abs(fontFactor - FontFactor) < 0.001f;
        if (unchanged && (factor == 1f && fontFactor == 1f || text != null))
            return;
        Factor = factor;
        FontFactor = fontFactor;
        DisposeFonts();
        if (factor == 1f && fontFactor == 1f)
            return;

        // The size is read on every build, so a rebuild after a change of Dalamud's font size picks up the new base.
        var atlas = Plugin.PluginInterface.UiBuilder.FontAtlas;
        text = atlas.NewDelegateFontHandle(e => e.OnPreBuild(tk => tk.AddDalamudDefaultFont(UiBuilder.DefaultFontSizePx * factor * fontFactor)));
        icons = atlas.NewDelegateFontHandle(e => e.OnPreBuild(tk => tk.AddFontAwesomeIconFont(new SafeFontConfig { SizePx = UiBuilder.DefaultFontSizePx * factor })));
    }

    /// <summary>Pushes a text font a quarter larger than the scaled one, built on first use.</summary>
    public static IDisposable PushLarge()
    {
        if (large == null || Math.Abs(largeFactor - Factor) > 0.001f)
        {
            large?.Dispose();
            largeFactor = Factor;
            var size = UiBuilder.DefaultFontSizePx * Factor * LargeText;
            large = Plugin.PluginInterface.UiBuilder.FontAtlas.NewDelegateFontHandle(e => e.OnPreBuild(tk => tk.AddDalamudDefaultFont(size)));
        }
        return large.Available ? large.Push() : NoFont.Instance;
    }

    /// <summary>Pushes the scaled text font; wrap a whole window in it.</summary>
    public static IDisposable PushText() => text != null && text.Available ? text.Push() : NoFont.Instance;

    /// <summary>Pushes the scaled FontAwesome font (Dalamud's icon font at 100 %).</summary>
    public static IDisposable PushIcons() => icons != null && icons.Available ? icons.Push() : ImRaii.PushFont(UiBuilder.IconFont);

    public static void Dispose()
    {
        DisposeFonts();
        large?.Dispose();
        large = null;
    }

    private static void DisposeFonts()
    {
        text?.Dispose();
        icons?.Dispose();
        text = icons = null;
    }

    private sealed class NoFont : IDisposable
    {
        public static readonly NoFont Instance = new();

        public void Dispose()
        {
        }
    }
}
