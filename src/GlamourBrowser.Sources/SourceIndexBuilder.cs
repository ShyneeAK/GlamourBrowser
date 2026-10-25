using GlamourBrowser.Core.Sets;
using GlamourBrowser.Core.Sources;
using Lumina;

namespace GlamourBrowser.Sources;

/// <summary>
/// Builds the source index from Lumina and LuminaSupplemental. Each source family runs in its own
/// try/catch: a failing family is logged and marked, the others still make it into the index.
/// A cancelled <paramref name="cancel"/> stops the build before the next family with an
/// <see cref="OperationCanceledException"/>.
/// </summary>
public sealed class SourceIndexBuilder(GameData data, IReadOnlySet<uint> relevantItems, Action<string> log, CancellationToken cancel = default)
{
    public SourceIndex Build()
    {
        var builder = new SourceIndex.Builder();
        // Coffers holding relevant items take part as well, so their sources can pass on to what they contain.
        IReadOnlyDictionary<uint, IReadOnlyList<uint>> coffers = new Dictionary<uint, IReadOnlyList<uint>>();
        Run(builder, "Kisten", () => coffers = CofferContents.Find(data, relevantItems, log));
        var relevant = relevantItems.Concat(coffers.Keys).ToHashSet();
        ShopNpcResolver? npcs = null;
        Run(builder, "Händler-NPCs", () => npcs = new ShopNpcResolver(data, log));
        if (npcs != null)
        {
            foreach (var failure in npcs.Failures)
                builder.MarkFailure(failure);
            IReadOnlySet<uint> jobGear = new HashSet<uint>();
            Run(builder, "Job-Ausrüstung", () => jobGear = JobGearItems.Find(data, relevant));
            Run(builder, "Gil-Händler", () => ShopSources.AddGilShops(builder, data, npcs, relevant, jobGear));
            Run(builder, "Staatliche Gesellschaft", () => ShopSources.AddGrandCompany(builder, data, npcs, relevant));
            Run(builder, "Spezialshops", () => ShopSources.AddSpecialShops(builder, data, npcs, relevant, jobGear));
        }
        Run(builder, "Inhalte", () => ContentSources.AddDuties(builder, data, relevant, log));
        Run(builder, "Inhalts-Ketten", () => ContentSources.AddDutyChains(builder, data, relevant, log));
        Run(builder, "Quests", () => QuestSources.AddQuests(builder, data, relevant));
        Run(builder, "Errungenschaften", () => QuestSources.AddAchievements(builder, data, relevant));
        Run(builder, "Relikte", () => QuestSources.AddRelics(builder, data, relevant, log));
        Run(builder, "Handwerk", () => MiscSources.AddRecipes(builder, data, relevant));
        Run(builder, "Mogstation", () => MiscSources.AddMogStation(builder, data, relevant, log));
        Run(builder, "Sonstiges", () => MiscSources.AddMisc(builder, data, relevant, log));
        Run(builder, "Patches", () => MiscSources.AddPatches(builder, data, relevant, log));
        Run(builder, "Kisten-Inhalt", () =>
        {
            var items = data.GetExcelSheet<Lumina.Excel.Sheets.Item>()!;
            builder.InheritFromContainers(coffers, id => items.TryGetRow(id, out var item) ? item.Name.ExtractText() : "");
        });
        Run(builder, "Varianten", () => builder.MergeVariants(VariantSheet.BaseOf(data)));
        Run(builder, "Serien", () =>
        {
            var (items, dresser, mogStation) = SetSheets.Read(data);
            builder.SetSets(SetDetection.Build(items, dresser, mogStation));
        });
        return builder.Build();
    }

    private void Run(SourceIndex.Builder builder, string family, Action action)
    {
        cancel.ThrowIfCancellationRequested();
        try
        {
            action();
        }
        catch (Exception e)
        {
            log($"Source family '{family}' failed: {e}");
            builder.MarkFailure(family);
        }
    }
}
