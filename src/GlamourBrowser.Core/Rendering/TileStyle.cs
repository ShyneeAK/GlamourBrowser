namespace GlamourBrowser.Core.Rendering;

/// <summary>Tint applied when drawing a tile (ImGui ABGR colour).</summary>
public static class TileStyle
{
    public const uint NoTint = 0xFFFFFFFF;

    /// <summary>Darker and slightly blue (R 0x70, G 0x78, B 0x90) for items the character does not own.</summary>
    public const uint MissingTint = 0xFF907870;

    public static uint TintFor(bool owned, bool dimMissing) => owned || !dimMissing ? NoTint : MissingTint;
}
