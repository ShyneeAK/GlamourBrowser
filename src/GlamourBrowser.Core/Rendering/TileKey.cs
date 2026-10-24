namespace GlamourBrowser.Core.Rendering;

/// <summary>Everything that changes how a tile looks.</summary>
public readonly record struct TileKey(uint ItemId, byte Stain0, byte Stain1, TileMode Mode, ulong AppearanceHash, int RenderWidth);
