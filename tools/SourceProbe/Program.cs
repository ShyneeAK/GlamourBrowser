// Offline check of the source index against the game data (not part of the plugin).
// Usage: SourceProbe <sqpack> [itemId ...] [--find "name part"] [--samples] [--sets] [--blocks] [--timeline [id ...]] [--accessories] [--dyes]
using System.Diagnostics;
using GlamourBrowser.Core.Items;
using GlamourBrowser.Core.Sets;
using GlamourBrowser.Core.Sources;
using GlamourBrowser.Sources;
using Lumina;
using Lumina.Excel.Sheets;

if (args.Length == 0)
{
    Console.WriteLine("SourceProbe <sqpack> [itemId ...] [--find \"name part\"]");
    return 1;
}

var data = new GameData(args[0], new LuminaOptions { PanicOnSheetChecksumMismatch = false, DefaultExcelLanguage = Lumina.Data.Language.German });
var items = data.GetExcelSheet<Item>()!;

var findIndex = Array.IndexOf(args, "--find");
if (findIndex >= 0 && findIndex + 1 < args.Length)
{
    foreach (var item in items.Where(i => i.EquipSlotCategory.RowId != 0 && i.Name.ExtractText().Contains(args[findIndex + 1], StringComparison.OrdinalIgnoreCase)).Take(20))
        Console.WriteLine($"{item.RowId}: {item.Name.ExtractText()}  ModelMain {item.ModelMain:X16} ModelSub {item.ModelSub:X16}");
    return 0;
}

if (args.Contains("--dyes"))
{
    // Every dye of the Stain sheet and the items that give it (the generic dye or a dye of its own).
    foreach (var stain in data.GetExcelSheet<Stain>()!.Where(s => !s.Name.IsEmpty))
        Console.WriteLine($"{stain.RowId}: {stain.Name.ExtractText()}  -> {string.Join(", ", stain.Item.Where(i => i.RowId != 0).Select(i => $"{i.RowId} {i.Value.Name.ExtractText()}"))}");
    return 0;
}

if (args.Contains("--blocks"))
{
    // Items that cover more than their own slot (robes over legs and feet, two-handed weapons), per slot and blocked slots.
    var covering = items.Where(i => i.EquipSlotCategory.RowId != 0 && i.ModelMain != 0)
        .Select(i => (Item: i, Slot: ItemSlots.SlotOf(i.EquipSlotCategory.Value), Blocks: ItemSlots.BlocksOf(i.EquipSlotCategory.Value)))
        .Where(x => x.Slot != null && x.Blocks != 0)
        .ToList();
    foreach (var group in covering.GroupBy(x => (x.Slot, x.Blocks)).OrderByDescending(g => g.Count()))
    {
        var blocked = string.Join(" ", Enum.GetValues<EquipSlot>().Where(s => SlotMask.Contains(group.Key.Blocks, s)));
        Console.WriteLine($"{group.Key.Slot} blocks {blocked}: {group.Count()} items, e.g. {string.Join(", ", group.Take(3).Select(x => x.Item.Name.ExtractText()))}");
    }
    return 0;
}

if (args.Contains("--accessories"))
{
    // Ring models per hand (_rir/_ril) and the side of bracelet models (bounding box x: + is the character's left).
    var (right, left, both, none) = (0, 0, 0, 0);
    foreach (var item in items.Where(i => i.ModelMain != 0 && ItemSlots.SlotOf(i.EquipSlotCategory.Value) is EquipSlot.RingRight or EquipSlot.RingLeft))
    {
        var set = item.ModelMain & 0xFFFF;
        bool Has(string suffix) => new[] { "c0101", "c0201" }.Any(race => data.FileExists($"chara/accessory/a{set:D4}/model/{race}a{set:D4}_{suffix}.mdl"));
        var (r, l) = (Has("rir"), Has("ril"));
        int Size(string suffix) => new[] { "c0101", "c0201" }.Select(race => data.GetFile($"chara/accessory/a{set:D4}/model/{race}a{set:D4}_{suffix}.mdl")?.Data.Length ?? 0).Max();
        var (rs, ls) = (Size("rir"), Size("ril"));
        if (Math.Min(rs, ls) * 3 < Math.Max(rs, ls))
            Console.WriteLine($"ring {item.RowId} {item.Name.ExtractText()} a{set:D4}: sizes right {rs}, left {ls}");
        if (r && l) both++; else if (r) right++; else if (l) left++; else none++;
        if (r != l)
            Console.WriteLine($"ring {item.RowId} {item.Name.ExtractText()} a{set:D4}: only {(r ? "right" : "left")}");
    }
    Console.WriteLine($"rings: both {both}, right only {right}, left only {left}, none {none}");

    var sides = new Dictionary<string, int>();
    foreach (var item in items.Where(i => i.ModelMain != 0 && ItemSlots.SlotOf(i.EquipSlotCategory.Value) == EquipSlot.Wrists))
    {
        var set = item.ModelMain & 0xFFFF;
        var file = new[] { "c0101", "c0201" }.Select(race => $"chara/accessory/a{set:D4}/model/{race}a{set:D4}_wrs.mdl").FirstOrDefault(data.FileExists);
        if (file == null)
        {
            sides["no model"] = sides.GetValueOrDefault("no model") + 1;
            continue;
        }
        // The bones the model is skinned to are named in its string block: a bracelet on one wrist names only that arm's.
        var text = System.Text.Encoding.ASCII.GetString(data.GetFile(file)!.Data);
        bool Names(string side) => text.Contains($"j_te_{side}\0") || text.Contains($"j_ude_b_{side}\0") || text.Contains($"n_hte_{side}\0");
        var (onRight, onLeft) = (Names("r"), Names("l"));
        var side = onRight && onLeft ? "both" : onRight ? "right" : onLeft ? "left" : "unknown";
        sides[side] = sides.GetValueOrDefault(side) + 1;
        if (side is "right" or "left" ? sides[side] <= 8 : side == "unknown")
            Console.WriteLine($"bracelet {item.RowId} {item.Name.ExtractText()} a{set:D4}: {side}");
    }
    Console.WriteLine("bracelets: " + string.Join(", ", sides.Select(kv => $"{kv.Key} {kv.Value}")));
    return 0;
}

var timelineIndex = Array.IndexOf(args, "--timeline");
if (timelineIndex >= 0)
{
    // ActionTimeline keys of ids seen in a trace, or all idle pose timelines without ids.
    var timelines = data.GetExcelSheet<ActionTimeline>()!;
    var ids = args.Skip(timelineIndex + 1).TakeWhile(a => uint.TryParse(a, out _)).Select(uint.Parse).ToList();
    var rows = ids.Count > 0 ? ids.Select(id => timelines.GetRow(id)) : timelines.Where(t => t.Key.ExtractText().StartsWith("emote/pose"));
    foreach (var row in rows)
    {
        var fields = typeof(ActionTimeline).GetProperties()
            .Where(f => f.Name is not ("Key" or "RowId" or "SubrowId" or "ExcelPage" or "RowOffset"))
            .Select(f => $"{f.Name}={FieldText(f.GetValue(row))}");
        Console.WriteLine($"{row.RowId}: {row.Key.ExtractText()} | {string.Join(" ", fields)}");
    }
    return 0;
}

var relevant = items.Where(i => i.EquipSlotCategory.RowId != 0 && i.ModelMain != 0).Select(i => i.RowId).ToHashSet();
var watch = Stopwatch.StartNew();
var index = new SourceIndexBuilder(data, relevant, Console.WriteLine).Build();
Console.WriteLine($"Built in {watch.ElapsedMilliseconds} ms for {relevant.Count} items");

if (args.Contains("--sets"))
{
    var sets = index.Sets;
    int Count(SetKind kind) => sets.Count(s => s.Id.Kind == kind);
    Console.WriteLine($"Sets: {sets.Count} (dresser {Count(SetKind.Dresser)}, mog station {Count(SetKind.MogStation)}, model {Count(SetKind.Model)})");
    for (var n = 0; n < 10 && sets.Count > 0; n++)
    {
        var sample = sets[n * sets.Count / 10];
        Console.WriteLine($"  {sample.Name} [{sample.Id.Kind}]: {string.Join(" | ", sample.Pieces.Select(p => items.GetRow(p.ItemId).Name.ExtractText()))}");
    }

    var passed = true;
    void Check(string what, bool ok)
    {
        Console.WriteLine($"check {what}: {(ok ? "ok" : "MISSING")}");
        passed &= ok;
    }
    EquipSlot[] armour = [EquipSlot.Head, EquipSlot.Body, EquipSlot.Hands, EquipSlot.Legs, EquipSlot.Feet];
    HashSet<uint> ArmourOf(GlamourSet set) => set.Pieces.Where(p => armour.Contains(p.Slot)).Select(p => p.ItemId).ToHashSet();
    var dresserArmour = sets.Where(s => s.Id.Kind == SetKind.Dresser).Select(ArmourOf).ToList();
    Check("Ao Oni set (45275) with 5 pieces", sets.Any(s => s.Id == new SetId(SetKind.Dresser, 45275) && s.Name == "Ao Oni-Set" && s.Pieces.Count == 5));
    Check("Asuran scouting set with 5 pieces", sets.Any(s => s.Name == "Asuran-Set des Spähens" && s.Pieces.Count == 5));
    Check("no model set with the armour of a dresser set",
        !sets.Where(s => s.Id.Kind == SetKind.Model).Any(m => dresserArmour.Any(d => d.SetEquals(ArmourOf(m)))));
    Console.WriteLine(index.HasFailures ? "Failures: " + string.Join(", ", index.Failures) : "No failures");
    return passed && !index.HasFailures ? 0 : 1;
}

foreach (var arg in args.Skip(1).Where(a => uint.TryParse(a, out _)))
{
    var id = uint.Parse(arg);
    Console.WriteLine($"\n{id} {items.GetRow(id).Name.ExtractText()} · patch {index.PatchOf(id)} · {string.Join(", ", index.CategoriesOf(id).Select(SourceCategories.DisplayName))}");
    foreach (var s in index.SourcesOf(id).Take(20))
        Console.WriteLine($"  [{SourceCategories.DisplayName(s.Category)}] {s.Title} · {s.Detail} · cost {s.CostAmount}x{s.CostItemId} · {s.Action}");
}

if (args.Contains("--samples"))
{
    foreach (var category in Enum.GetValues<SourceCategory>())
    {
        Console.WriteLine($"\n== {SourceCategories.DisplayName(category)}");
        foreach (var id in relevant.Where(i => index.SourcesOf(i).Any(s => s.Category == category)).OrderBy(i => i).Where((_, n) => n % 97 == 0).Take(3))
        {
            var s = index.SourcesOf(id).First(x => x.Category == category);
            Console.WriteLine($"  {id} {items.GetRow(id).Name.ExtractText()}: {s.Title} · {s.Detail} · cost {s.CostAmount}x{s.CostItemId} · {s.Action}");
        }
    }
}

Console.WriteLine("\nCounts:");
foreach (var (category, count) in index.Counts(relevant).OrderBy(kv => kv.Key))
    Console.WriteLine($"  {SourceCategories.DisplayName(category)}: {count}");
Console.WriteLine(index.HasFailures ? "Failures: " + string.Join(", ", index.Failures) : "No failures");
return 0;

static string FieldText(object? value) => value?.GetType().GetProperty("RowId")?.GetValue(value)?.ToString() ?? value?.ToString() ?? "";
