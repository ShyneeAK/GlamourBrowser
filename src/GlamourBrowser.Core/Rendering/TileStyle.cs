namespace GlamourBrowser.Core.Rendering;

/// <param name="Color">ImGui ABGR colour.</param>
public readonly record struct TileFrame(uint Color, float Thickness, bool OwnedBadge);

/// <summary>How a tile's frame shows ownership and selection; the rendered character is never tinted.</summary>
public static class TileStyle
{
    public const uint OwnedColor = 0xFF40C040;
    public const uint MissingColor = 0xFF505050;
    public const uint SelectedColor = 0xFFFFFFFF;

    public static TileFrame FrameFor(bool owned, bool selected)
    {
        if (selected)
            return new TileFrame(SelectedColor, 3f, owned);
        return owned ? new TileFrame(OwnedColor, 2f, true) : new TileFrame(MissingColor, 1f, false);
    }
}
