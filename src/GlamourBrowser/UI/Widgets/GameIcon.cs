using System;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Textures;
using Dalamud.Interface.Utility.Raii;

namespace GlamourBrowser.UI.Widgets;

/// <summary>Draws a game icon (or a part of a game texture) into a rectangle, or a FontAwesome glyph when it is missing.</summary>
public static class GameIcon
{
    /// <param name="tint">Multiplies the game icon, e.g. to fade it; the fallback glyph uses <paramref name="fallbackColor"/>.</param>
    public static void DrawAt(ImDrawListPtr drawList, UiIcon icon, Vector2 min, Vector2 size, FontAwesomeIcon fallback, uint fallbackColor,
                              uint tint = 0xFFFFFFFF)
    {
        if (icon.Texture != null && Plugin.TextureProvider.GetFromGame(icon.Texture).TryGetWrap(out var part, out _))
        {
            drawList.AddImage(part.Handle, min, min + size, icon.Uv0, icon.Uv1, tint);
            return;
        }
        if (icon.IconId != 0 && Plugin.TextureProvider.GetFromGameIcon(new GameIconLookup(icon.IconId)).TryGetWrap(out var wrap, out _))
        {
            drawList.AddImage(wrap.Handle, min, min + size, Vector2.Zero, Vector2.One, tint);
            return;
        }

        using (UiScale.PushIcons())
        {
            // A box larger than the icon font (large buttons) gets a larger glyph.
            var glyph = fallback.ToIconString();
            var fontSize = ImGui.GetFontSize() * MathF.Max(1, MathF.Min(size.X, size.Y) / ImGui.GetFontSize());
            var textSize = ImGui.CalcTextSize(glyph) * (fontSize / ImGui.GetFontSize());
            drawList.AddText(ImGui.GetFont(), fontSize, min + (size - textSize) / 2, fallbackColor, glyph);
        }
    }

    public static void Draw(UiIcon icon, Vector2 size, FontAwesomeIcon fallback, uint fallbackColor)
    {
        var min = ImGui.GetCursorScreenPos();
        ImGui.Dummy(size);
        DrawAt(ImGui.GetWindowDrawList(), icon, min, size, fallback, fallbackColor);
    }
}
