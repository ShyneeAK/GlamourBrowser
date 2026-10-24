namespace GlamourBrowser.Core.Items;

public sealed record GlamourItem(
    uint Id,
    string Name,
    uint IconId,
    EquipSlot Slot,
    IReadOnlySet<uint> ClassJobIds,
    int LevelEquip,
    int DyeCount,
    RaceGenderRestriction Restriction);
