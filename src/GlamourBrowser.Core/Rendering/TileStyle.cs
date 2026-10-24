namespace GlamourBrowser.Core.Rendering;

/// <param name="Color">Frame colour (ImGui ABGR).</param>
/// <param name="BadgeColor">Colour of the corner badge that shows ownership.</param>
public readonly record struct TileFrame(uint Color, float Thickness, uint BadgeColor);

/// <summary>How a tile's frame shows ownership and selection; the rendered character is never tinted.</summary>
public static class TileStyle
{
    public const uint OwnedColor = 0xFF40C040;
    public const uint MissingColor = 0xFF808080;
    public const uint SelectedColor = 0xFFFFFFFF;

    public static TileFrame FrameFor(bool owned, bool selected)
    {
        var badge = owned ? OwnedColor : MissingColor;
        return selected ? new TileFrame(SelectedColor, 3f, badge) : new TileFrame(badge, 2f, badge);
    }
}
