// Offline check of the source index against the game data (not part of the plugin).
// Usage: SourceProbe <sqpack> [itemId ...] [--find "name part"]
using System.Diagnostics;
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
        Console.WriteLine($"{item.RowId}: {item.Name.ExtractText()}");
    return 0;
}

var relevant = items.Where(i => i.EquipSlotCategory.RowId != 0 && i.ModelMain != 0).Select(i => i.RowId).ToHashSet();
var watch = Stopwatch.StartNew();
var index = new SourceIndexBuilder(data, relevant, Console.WriteLine).Build();
Console.WriteLine($"Built in {watch.ElapsedMilliseconds} ms for {relevant.Count} items");

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
