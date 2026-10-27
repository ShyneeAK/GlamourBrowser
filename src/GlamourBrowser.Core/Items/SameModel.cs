namespace GlamourBrowser.Core.Items;

/// <summary>
/// Items with the same model in the same slot: the same shape, in any of its variants (another colouring or material),
/// such as the job versions of one design.
/// </summary>
public static class SameModel
{
    /// <returns>
    /// For every item that shares its model with at least one other, all items of that model (itself included), the same
    /// variant together, lowest level first, then by name; items without a model share nothing.
    /// </returns>
    public static IReadOnlyDictionary<uint, IReadOnlyList<GlamourItem>> Groups(IEnumerable<GlamourItem> items)
    {
        var result = new Dictionary<uint, IReadOnlyList<GlamourItem>>();
        foreach (var group in items.Where(i => i.ModelMain != 0).GroupBy(i => (i.Slot, Main: Shape(i.Slot, i.ModelMain), Sub: Shape(i.Slot, i.ModelSub))))
        {
            var model = group.OrderBy(i => i.ModelMain).ThenBy(i => i.ModelSub).ThenBy(i => i.LevelEquip)
                             .ThenBy(i => i.Name, StringComparer.CurrentCulture).ThenBy(i => i.Id).ToList();
            if (model.Count < 2)
                continue;
            foreach (var item in model)
                result[item.Id] = model;
        }
        return result;
    }

    /// <summary>
    /// The model without its variant. Gear and accessories keep the model in the low 16 bits (the variant above); weapons
    /// and shields their skeleton and base in the low 32 bits (the variant above those).
    /// </summary>
    public static ulong Shape(EquipSlot slot, ulong model)
        => model & (slot is EquipSlot.MainHand or EquipSlot.OffHand ? 0xFFFF_FFFFUL : 0xFFFFUL);
}
