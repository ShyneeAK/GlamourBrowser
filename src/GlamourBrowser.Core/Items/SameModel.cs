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
    /// The first item of each model, in the given order; items without a model each stay. Hides the other items of a model
    /// ("Modell-Duplikate ausblenden"), which the model button of the tile still shows.
    /// </summary>
    public static IReadOnlyList<GlamourItem> FirstOfEach(IEnumerable<GlamourItem> items) => items.DistinctBy(Key).ToList();

    /// <summary>Number of models per category, with the duplicates hidden: a model counts when one of its items has the category.</summary>
    public static IReadOnlyDictionary<TCategory, int> Counts<TCategory>(IEnumerable<GlamourItem> items, Func<uint, IEnumerable<TCategory>> categoriesOf)
        where TCategory : notnull
        => items.SelectMany(i => categoriesOf(i.Id).Select(c => (Category: c, Model: Key(i))))
                .Distinct()
                .GroupBy(x => x.Category)
                .ToDictionary(g => g.Key, g => g.Count());

    /// <summary>What items of one model share (an item without a model only with itself).</summary>
    private static (EquipSlot Slot, ulong Main, ulong Sub, uint Own) Key(GlamourItem item)
        => item.ModelMain == 0 ? (item.Slot, 0, 0, item.Id) : (item.Slot, Shape(item.Slot, item.ModelMain), Shape(item.Slot, item.ModelSub), 0);

    /// <summary>
    /// The model without its variant. Gear and accessories keep the model in the low 16 bits (the variant above); weapons
    /// and shields their skeleton and base in the low 32 bits (the variant above those).
    /// </summary>
    public static ulong Shape(EquipSlot slot, ulong model)
        => model & (slot is EquipSlot.MainHand or EquipSlot.OffHand ? 0xFFFF_FFFFUL : 0xFFFFUL);
}
