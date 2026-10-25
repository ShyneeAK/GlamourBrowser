using System.Text.RegularExpressions;

namespace GlamourBrowser.Core.Items;

public sealed record VariantCandidate(uint ItemId, string Name, ulong ModelMain, ulong ModelSub);

/// <summary>
/// Items whose name only adds " +1", " +2" … to another item's name and that look the same (identical models) are
/// variants of it: the browser shows the base item once, and owning any variant counts as owning it.
/// </summary>
public static class ItemVariants
{
    private static readonly Regex Plus = new(@"\s*\+\d+$", RegexOptions.Compiled);

    /// <returns>Every merged "+N" variant mapped to its base item (the one without "+N", else the lowest id).</returns>
    public static IReadOnlyDictionary<uint, uint> BaseOf(IEnumerable<VariantCandidate> items)
    {
        var result = new Dictionary<uint, uint>();
        foreach (var group in items.GroupBy(i => (Name: Plus.Replace(i.Name, ""), i.ModelMain, i.ModelSub)))
        {
            var variants = group.Where(i => Plus.IsMatch(i.Name)).ToList();
            if (variants.Count == 0)
                continue;
            var baseItem = group.Where(i => !Plus.IsMatch(i.Name)).MinBy(i => i.ItemId) ?? variants.MinBy(i => i.ItemId)!;
            foreach (var variant in variants.Where(v => v.ItemId != baseItem.ItemId))
                result[variant.ItemId] = baseItem.ItemId;
        }
        return result;
    }
}
