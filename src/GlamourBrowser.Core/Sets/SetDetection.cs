using GlamourBrowser.Core.Items;
using static GlamourBrowser.Core.Items.EquipSlot;

namespace GlamourBrowser.Core.Sets;

/// <summary>An equippable item as set detection sees it (from the Item sheet).</summary>
/// <param name="ModelSet">(ushort)ModelMain.</param>
/// <param name="ModelVariant">(byte)(ModelMain &gt;&gt; 16).</param>
public sealed record SetCandidate(uint ItemId, string Name, EquipSlot Slot, ushort ModelSet, byte ModelVariant, uint ItemLevel,
                                  uint ClassJobCategory, IReadOnlySet<uint> Jobs, int LevelEquip);

/// <summary>A set the game lists itself (glamour dresser outfit, mog station set).</summary>
public sealed record SheetSet(SetId Id, string Name, IReadOnlyList<uint> ItemIds);

/// <summary>
/// Finds the sets of the game: dresser outfits, mog station sets and model series (armour sharing model, variant,
/// item level and job category, plus matching accessories). Model series already covered by a dresser outfit and
/// mog station sets equal to one are dropped.
/// </summary>
public static class SetDetection
{
    private static readonly EquipSlot[] Armour = [Head, Body, Hands, Legs, Feet];
    private static readonly EquipSlot[] Accessories = [Ears, Neck, Wrists, RingRight];
    private const int MinArmourSlots = 3;
    private const int MinPieces = 2;

    public static IReadOnlyList<GlamourSet> Build(IReadOnlyList<SetCandidate> items, IReadOnlyList<SheetSet> dresser, IReadOnlyList<SheetSet> mogStation)
    {
        var byId = new Dictionary<uint, SetCandidate>();
        foreach (var item in items)
            byId.TryAdd(item.ItemId, item);

        var result = dresser.Select(s => FromSheet(s, byId)).OfType<GlamourSet>().ToList();
        var dresserItems = result.Select(s => s.Pieces.Select(p => p.ItemId).ToHashSet()).ToList();

        foreach (var series in ModelSeries(items))
        {
            var armour = series.Pieces.Where(p => Armour.Contains(p.Slot)).Select(p => p.ItemId).ToList();
            if (!dresserItems.Any(outfit => armour.All(outfit.Contains)))
                result.Add(series);
        }

        foreach (var set in mogStation.Select(s => FromSheet(s, byId)).OfType<GlamourSet>())
        {
            var ids = set.Pieces.Select(p => p.ItemId).ToHashSet();
            if (!dresserItems.Any(outfit => outfit.SetEquals(ids)))
                result.Add(set);
        }
        return result;
    }

    private static GlamourSet? FromSheet(SheetSet sheet, IReadOnlyDictionary<uint, SetCandidate> byId)
    {
        // Pieces the catalog does not know drop out; the first piece of a slot wins.
        var pieces = sheet.ItemIds.Select(id => byId.GetValueOrDefault(id)).OfType<SetCandidate>()
            .GroupBy(i => i.Slot).Select(slot => slot.First()).OrderBy(i => i.Slot).ToList();
        if (pieces.Count < MinPieces)
            return null;
        var name = sheet.Name.Length > 0
            ? sheet.Name
            : SetNames.FromCommonStart(pieces.Select(p => p.Name).ToList()) ?? SetNames.Fallback(pieces[0].Name);
        return Create(sheet.Id, name, pieces);
    }

    private static IEnumerable<GlamourSet> ModelSeries(IReadOnlyList<SetCandidate> items)
    {
        var accessories = items.Where(i => Accessories.Contains(i.Slot)).ToLookup(i => i.ItemLevel);
        foreach (var group in items.Where(i => Armour.Contains(i.Slot)).GroupBy(i => (i.ModelSet, i.ModelVariant, i.ItemLevel, i.ClassJobCategory)))
        {
            var armour = PickPerSlot(group);
            if (armour.Count < MinArmourSlots)
                continue;

            var names = armour.Select(p => p.Name).ToList();
            var start = SetNames.CommonStart(names);
            var pieces = new List<SetCandidate>(armour);
            // Accessories have models of their own: they join by item level, name start and jobs.
            if (start.Length > 0)
            {
                pieces.AddRange(PickPerSlot(accessories[group.Key.ItemLevel]
                    .Where(a => a.Name.StartsWith(start, StringComparison.Ordinal) && armour.All(p => p.Jobs.IsSubsetOf(a.Jobs)))));
            }

            var body = armour.FirstOrDefault(p => p.Slot == Body) ?? armour.OrderBy(p => p.Slot).First();
            var name = SetNames.FromCommonStart(names) ?? SetNames.Fallback(body.Name);
            yield return Create(new SetId(SetKind.Model, pieces.Min(p => p.ItemId)), name, pieces);
        }
    }

    /// <summary>One item per slot: one without the old-item marker "†" first, then the lowest id.</summary>
    private static List<SetCandidate> PickPerSlot(IEnumerable<SetCandidate> items)
        => items.GroupBy(i => i.Slot).Select(slot => slot.OrderBy(i => i.Name.Contains('†')).ThenBy(i => i.ItemId).First()).ToList();

    private static GlamourSet Create(SetId id, string name, IReadOnlyList<SetCandidate> pieces)
        => new(id, name, pieces.OrderBy(p => p.Slot).Select(p => new SetPiece(p.ItemId, p.Slot)).ToList(), pieces.Max(p => p.LevelEquip));
}
