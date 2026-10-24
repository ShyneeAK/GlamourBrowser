namespace GlamourBrowser.Core.Rendering;

/// <param name="Color">Frame colour (ImGui ABGR).</param>
/// <param name="BadgeColor">Colour of the corner badge that shows ownership.</param>
public readonly record struct TileFrame(uint Color, float Thickness, uint BadgeColor);

/// <summary>How a tile shows ownership and selection: frame, corner badge and a slight dimming of missing items.</summary>
public static class TileStyle
{
    public const uint OwnedColor = 0xFF40C040;
    public const uint MissingColor = 0xFF808080;

    /// <summary>65 % brightness for missing items; owned and selected tiles are drawn untinted.</summary>
    public const uint MissingTint = 0xFFA6A6A6;

    public static uint ImageTint(bool owned, bool selected) => owned || selected ? 0xFFFFFFFF : MissingTint;

    /// <param name="selectedColor">The theme's accent colour, used for the selection frame.</param>
    public static TileFrame FrameFor(bool owned, bool selected, uint selectedColor)
    {
        var badge = owned ? OwnedColor : MissingColor;
        return selected ? new TileFrame(selectedColor, 3f, badge) : new TileFrame(badge, 2f, badge);
    }
}
