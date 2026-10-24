using GlamourBrowser.Core.Sources;
using Lumina;

namespace GlamourBrowser.Sources;

/// <summary>
/// Builds the source index from Lumina and LuminaSupplemental. Each source family runs in its own
/// try/catch: a failing family is logged and marked, the others still make it into the index.
/// </summary>
public sealed class SourceIndexBuilder(GameData data, IReadOnlySet<uint> relevantItems, Action<string> log)
{
    public SourceIndex Build()
    {
        var builder = new SourceIndex.Builder();
        ShopNpcResolver? npcs = null;
        Run(builder, "Händler-NPCs", () => npcs = new ShopNpcResolver(data, log));
        if (npcs != null)
        {
            Run(builder, "Gil-Händler", () => ShopSources.AddGilShops(builder, data, npcs, relevantItems));
            Run(builder, "Staatliche Gesellschaft", () => ShopSources.AddGrandCompany(builder, data, npcs, relevantItems));
            Run(builder, "Spezialshops", () => ShopSources.AddSpecialShops(builder, data, npcs, relevantItems));
        }
        return builder.Build();
    }

    private void Run(SourceIndex.Builder builder, string family, Action action)
    {
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
