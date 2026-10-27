namespace GlamourBrowser.Core.Items;

/// <summary>Items that look the same: the same slot and the same models (dyes aside), such as job versions of one design.</summary>
public static class SameModel
{
    /// <returns>
    /// For every item that shares its look with at least one other, all items of that look (itself included), lowest
    /// level first, then by name; items without a model share nothing.
    /// </returns>
    public static IReadOnlyDictionary<uint, IReadOnlyList<GlamourItem>> Groups(IEnumerable<GlamourItem> items)
    {
        var result = new Dictionary<uint, IReadOnlyList<GlamourItem>>();
        foreach (var group in items.Where(i => i.ModelMain != 0).GroupBy(i => (i.Slot, i.ModelMain, i.ModelSub)))
        {
            var look = group.OrderBy(i => i.LevelEquip).ThenBy(i => i.Name, StringComparer.CurrentCulture).ThenBy(i => i.Id).ToList();
            if (look.Count < 2)
                continue;
            foreach (var item in look)
                result[item.Id] = look;
        }
        return result;
    }
}
