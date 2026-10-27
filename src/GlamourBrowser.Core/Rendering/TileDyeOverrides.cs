using GlamourBrowser.Core.Items;

namespace GlamourBrowser.Core.Rendering;

/// <summary>
/// Dyes chosen for single item tiles (the dye boxes on the tile) instead of the tile dye above the grid. A choice equal
/// to the tile dye is no override; a new tile dye replaces them all.
/// </summary>
public sealed class TileDyeOverrides
{
    private readonly Dictionary<uint, DyePair> overrides = new();

    /// <summary>The item tile's dye: its own, else the tile dye.</summary>
    public DyePair For(uint itemId, DyePair tileDye) => overrides.GetValueOrDefault(itemId, tileDye);

    /// <summary>Whether the item tile has a dye of its own.</summary>
    public bool Has(uint itemId) => overrides.ContainsKey(itemId);

    public void Set(uint itemId, DyePair dye, DyePair tileDye)
    {
        if (dye == tileDye)
            overrides.Remove(itemId);
        else
            overrides[itemId] = dye;
    }

    public void Clear() => overrides.Clear();
}
