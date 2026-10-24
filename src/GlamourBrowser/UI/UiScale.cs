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

    public static float Factor { get; private set; } = 1f;

    /// <summary>Dalamud's global scale times the plugin's factor, for fixed pixel sizes.</summary>
    public static float Global => ImGuiHelpers.GlobalScale * Factor;

    public static void Set(float factor)
    {
        factor = Math.Clamp(factor, Min, Max);
        if (Math.Abs(factor - Factor) < 0.001f && (factor == 1f || text != null))
            return;
        Factor = factor;
        DisposeFonts();
        if (factor == 1f)
            return;

        var size = UiBuilder.DefaultFontSizePx * factor;
        var atlas = Plugin.PluginInterface.UiBuilder.FontAtlas;
        text = atlas.NewDelegateFontHandle(e => e.OnPreBuild(tk => tk.AddDalamudDefaultFont(size)));
        icons = atlas.NewDelegateFontHandle(e => e.OnPreBuild(tk => tk.AddFontAwesomeIconFont(new SafeFontConfig { SizePx = size })));
    }

    /// <summary>Pushes the scaled text font; wrap a whole window in it.</summary>
    public static IDisposable PushText() => text != null && text.Available ? text.Push() : NoFont.Instance;

    /// <summary>Pushes the scaled FontAwesome font (Dalamud's icon font at 100 %).</summary>
    public static IDisposable PushIcons() => icons != null && icons.Available ? icons.Push() : ImRaii.PushFont(UiBuilder.IconFont);

    public static void Dispose() => DisposeFonts();

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
