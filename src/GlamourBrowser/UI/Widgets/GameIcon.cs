using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Textures;
using Dalamud.Interface.Utility.Raii;

namespace GlamourBrowser.UI.Widgets;

/// <summary>Draws a game icon into a rectangle, or a FontAwesome glyph when the icon is missing.</summary>
public static class GameIcon
{
    public static void DrawAt(ImDrawListPtr drawList, uint iconId, Vector2 min, Vector2 size, FontAwesomeIcon fallback, uint fallbackColor)
    {
        if (iconId != 0 && Plugin.TextureProvider.GetFromGameIcon(new GameIconLookup(iconId)).TryGetWrap(out var wrap, out _))
        {
            drawList.AddImage(wrap.Handle, min, min + size);
            return;
        }

        using (ImRaii.PushFont(UiBuilder.IconFont))
        {
            var glyph = fallback.ToIconString();
            var textSize = ImGui.CalcTextSize(glyph);
            drawList.AddText(min + (size - textSize) / 2, fallbackColor, glyph);
        }
    }

    public static void Draw(uint iconId, Vector2 size, FontAwesomeIcon fallback, uint fallbackColor)
    {
        var min = ImGui.GetCursorScreenPos();
        ImGui.Dummy(size);
        DrawAt(ImGui.GetWindowDrawList(), iconId, min, size, fallback, fallbackColor);
    }
}
