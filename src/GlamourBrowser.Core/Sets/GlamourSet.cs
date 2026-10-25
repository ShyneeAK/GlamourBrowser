using GlamourBrowser.Core.Items;

namespace GlamourBrowser.Core.Sets;

public readonly record struct SetPiece(uint ItemId, EquipSlot Slot);

/// <param name="Pieces">At most one piece per slot, in slot order.</param>
/// <param name="Level">The highest equip level among the pieces.</param>
public sealed record GlamourSet(SetId Id, string Name, IReadOnlyList<SetPiece> Pieces, int Level);
