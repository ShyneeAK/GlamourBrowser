using GlamourBrowser.Core.Sources;
using Lumina;
using Lumina.Excel.Sheets;

namespace GlamourBrowser.Sources;

/// <summary>Gil shops, grand company shops and special shops (tomestones, scrips, tokens, MGP, PvP …).</summary>
public static class ShopSources
{
    private const uint GcSealItem = 20;

    public static void AddGilShops(SourceIndex.Builder builder, GameData data, ShopNpcResolver npcs, IReadOnlySet<uint> relevant)
    {
        var items = data.GetSubrowExcelSheet<GilShopItem>()!;
        foreach (var shop in data.GetExcelSheet<GilShop>()!)
        {
            if (!items.TryGetRow(shop.RowId, out var entries))
                continue;
            foreach (var entry in entries)
            {
                var itemId = entry.Item.RowId;
                if (!relevant.Contains(itemId) || !entry.Item.IsValid)
                    continue;
                AddVendor(builder, npcs, itemId, shop.RowId, shop.Name.ExtractText(), SourceCategory.GilVendor, CurrencyCategory.Gil, entry.Item.Value.PriceMid);
            }
        }
    }

    public static void AddGrandCompany(SourceIndex.Builder builder, GameData data, ShopNpcResolver npcs, IReadOnlySet<uint> relevant)
    {
        var categories = data.GetExcelSheet<GCScripShopCategory>()!;
        var items = data.GetSubrowExcelSheet<GCScripShopItem>()!;
        foreach (var gcShop in data.GetExcelSheet<GCShop>()!)
        {
            var companyName = gcShop.GrandCompany.IsValid ? gcShop.GrandCompany.Value.Name.ExtractText() : "Staatliche Gesellschaft";
            foreach (var category in categories.Where(c => c.GrandCompany.RowId == gcShop.GrandCompany.RowId))
            {
                if (!items.TryGetRow(category.RowId, out var entries))
                    continue;
                foreach (var entry in entries)
                {
                    if (relevant.Contains(entry.Item.RowId))
                        AddVendor(builder, npcs, entry.Item.RowId, gcShop.RowId, companyName, SourceCategory.GrandCompany, GcSealItem, entry.CostGCSeals);
                }
            }
        }
    }

    public static void AddSpecialShops(SourceIndex.Builder builder, GameData data, ShopNpcResolver npcs, IReadOnlySet<uint> relevant)
    {
        var tomestoneRows = data.GetExcelSheet<TomestonesItem>()!.ToList();
        var tomestoneIds = tomestoneRows.Select(t => t.Item.RowId).ToHashSet();
        var ordered = tomestoneRows.Where(t => t.Tomestones.RowId > 0).OrderBy(t => t.Tomestones.RowId).Select(t => t.Item.RowId).ToList();

        foreach (var shop in data.GetExcelSheet<SpecialShop>()!)
        {
            foreach (var entry in shop.Item)
            {
                // Explicit loop: a default(ItemCostsStruct) from FirstOrDefault throws on access.
                uint costItem = 0, costAmount = 0;
                foreach (var cost in entry.ItemCosts)
                {
                    if (cost.ItemCost.RowId == 0)
                        continue;
                    costItem = cost.ItemCost.RowId;
                    costAmount = cost.CurrencyCost;
                    break;
                }
                var currency = costItem == 0 ? 0 : SpecialShopCost.ResolveItemId(costItem, shop.UseCurrencyType, ordered);
                var category = CurrencyCategory.From(currency, tomestoneIds);

                foreach (var receive in entry.ReceiveItems)
                {
                    var itemId = receive.Item.RowId;
                    if (relevant.Contains(itemId))
                        AddVendor(builder, npcs, itemId, shop.RowId, shop.Name.ExtractText(), category, currency, costAmount);
                }
            }
        }
    }

    /// <summary>One source per NPC selling the shop; the shop name stands in when no NPC is known.</summary>
    private static void AddVendor(SourceIndex.Builder builder, ShopNpcResolver npcs, uint itemId, uint shopId, string shopName,
                                  SourceCategory category, uint costItemId, uint costAmount)
    {
        var sellers = npcs.NpcsForShop(shopId);
        if (sellers.Count == 0)
        {
            builder.Add(itemId, new ItemSource(category, shopName.Length > 0 ? shopName : "Händler", "Ort unbekannt", null, costItemId, costAmount));
            return;
        }

        foreach (var npc in sellers)
        {
            var place = npcs.PlaceOf(npc);
            var name = npcs.NameOf(npc);
            var detail = place == null ? "Ort unbekannt" : $"{place.PlaceName} ({place.MapPosition.X:0.0}, {place.MapPosition.Y:0.0})";
            SourceAction? action = place == null ? null : new MapFlag(place.TerritoryId, place.MapId, place.MapPosition.X, place.MapPosition.Y);
            builder.Add(itemId, new ItemSource(category, name.Length > 0 ? name : shopName, detail, action, costItemId, costAmount));
        }
    }
}
