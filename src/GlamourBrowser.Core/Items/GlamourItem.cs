namespace GlamourBrowser.Core.Items;

/// <param name="Blocks">Slots the item covers besides its own (a <see cref="SlotMask"/>): a robe over legs and feet, a two-handed weapon over the off hand.</param>
public sealed record GlamourItem(
    uint Id,
    string Name,
    uint IconId,
    EquipSlot Slot,
    IReadOnlySet<uint> ClassJobIds,
    int LevelEquip,
    int DyeCount,
    RaceGenderRestriction Restriction,
    Tradability Trade = Tradability.Bound,
    ushort Blocks = 0)
{
    /// <summary>Its own slot and the ones it blocks: what it replaces on a character.</summary>
    public ushort Covers => (ushort)(SlotMask.Of(Slot) | Blocks);
}
