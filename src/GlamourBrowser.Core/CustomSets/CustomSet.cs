using GlamourBrowser.Core.Items;

namespace GlamourBrowser.Core.CustomSets;

/// <summary>One slot of a custom set: the item and its two dye channels (0 = no dye).</summary>
public readonly record struct CustomPiece(EquipSlot Slot, uint ItemId, byte Stain0, byte Stain1);

/// <summary>A set the player put together, like an outfit in the glamour dresser.</summary>
/// <param name="Pieces">At most one piece per slot, in slot order.</param>
public sealed record CustomSet(uint Id, string Name, IReadOnlyList<CustomPiece> Pieces, DateTimeOffset Changed)
{
    /// <summary>The folder the set is in (<see cref="CustomFolder.Id"/>); 0 for none. Only moving it changes it.</summary>
    public uint Folder { get; init; }

    /// <summary>Same name and pieces; the id and the time of the last change do not count.</summary>
    public bool SameContent(CustomSet other) => Name == other.Name && Pieces.SequenceEqual(other.Pieces);
}

/// <summary>A folder of custom sets the player named; one level, it holds sets only.</summary>
public sealed record CustomFolder(uint Id, string Name);
