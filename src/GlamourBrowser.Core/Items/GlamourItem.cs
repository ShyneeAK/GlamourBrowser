namespace GlamourBrowser.Core.Items;

/// <param name="Blocks">Slots the item covers besides its own (a <see cref="SlotMask"/>): a robe over legs and feet, a two-handed weapon over the off hand.</param>
/// <param name="ModelMain">The game's model of the item (model, base and variant); items with the same models look the same.</param>
/// <param name="ModelSub">The second model of a weapon (the off-hand part of a pair), else 0.</param>
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
    ushort Blocks = 0,
    ulong ModelMain = 0,
    ulong ModelSub = 0)
{
    /// <summary>Its own slot and the ones it blocks: what it replaces on a character.</summary>
    public ushort Covers => (ushort)(SlotMask.Of(Slot) | Blocks);
}
