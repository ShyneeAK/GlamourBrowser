using System.Numerics;
using System.Text.RegularExpressions;
using GlamourBrowser.Core.Sources;
using Lumina;
using Lumina.Excel.Sheets;
using LuminaSupplemental.Excel.Model;
using LuminaSupplemental.Excel.Services;

namespace GlamourBrowser.Sources;

public sealed record NpcPlace(uint TerritoryId, uint MapId, Vector2 MapPosition, string PlaceName);

/// <summary>
/// Which NPCs sell a shop and where they stand. Shops are reached from ENpcBase.ENpcData directly or via
/// TopicSelect, PreHandler, CustomTalk and InclusionShop; FateShop rows share their id with the NPC.
/// LuminaSupplemental adds a few special cases (ENpcShop). Positions come from the Level sheet (world
/// coordinates converted to map coordinates), with ENpcPlace (already map coordinates) filling gaps.
/// </summary>
public sealed class ShopNpcResolver
{
    private const uint GilShopType = 0x0004;
    private const uint SpecialShopType = 0x001B;

    private readonly Dictionary<uint, List<uint>> npcsByShop = new();
    private readonly Dictionary<uint, NpcPlace> places = new();
    private readonly GameData data;

    public ShopNpcResolver(GameData data, Action<string> log)
    {
        this.data = data;
        Step("Händler-NPCs", MapNpcData, log);
        Step("FATE-Händler", MapFateShops, log);
        Step("Händler-Zuordnung (Supplemental)", () => MapSupplementalShops(log), log);
        Step("Händler-Orte", LoadLevelPlaces, log);
        Step("Händler-Orte (Supplemental)", () => LoadSupplementalPlaces(log), log);
    }

    /// <summary>Steps that threw; the resolver still works with what the other steps found.</summary>
    public IReadOnlyList<string> Failures => failures;

    private readonly List<string> failures = new();

    private void Step(string name, System.Action step, Action<string> log)
    {
        try
        {
            step();
        }
        catch (Exception e)
        {
            log($"Shop resolver step '{name}' failed: {e}");
            failures.Add(name);
        }
    }

    public IReadOnlyList<uint> NpcsForShop(uint shopId) => npcsByShop.TryGetValue(shopId, out var list) ? list : [];

    public NpcPlace? PlaceOf(uint npcId) => places.GetValueOrDefault(npcId);

    // German names carry grammar markers such as "[p]" or "[a]".
    private static readonly Regex GrammarMarker = new(@"\[[a-z]\]", RegexOptions.Compiled);

    public string NameOf(uint npcId)
        => data.GetExcelSheet<ENpcResident>()!.TryGetRow(npcId, out var resident) ? GrammarMarker.Replace(resident.Singular.ExtractText(), "") : "";

    private void Link(uint shopId, uint npcId)
    {
        if (shopId == 0)
            return;
        if (!npcsByShop.TryGetValue(shopId, out var list))
            npcsByShop[shopId] = list = new List<uint>();
        if (!list.Contains(npcId))
            list.Add(npcId);
    }

    private static bool IsType(uint handler, uint type) => (handler >> 16) == type;

    private void MapNpcData()
    {
        var specialShops = data.GetExcelSheet<SpecialShop>()!;
        var firstSpecial = specialShops.First().RowId;
        var lastSpecial = specialShops.Last().RowId;

        foreach (var npc in data.GetExcelSheet<ENpcBase>()!)
        {
            foreach (var handlerRef in npc.ENpcData)
            {
                var handler = handlerRef.RowId;
                if (handler == 0)
                    continue;

                // Direct shops (gil, special, grand company): the handler id is the shop row id.
                Link(handler, npc.RowId);
                AddCustomTalk(handler, npc.RowId, firstSpecial, lastSpecial);
                AddTopicSelect(handler, npc.RowId);
                AddPreHandler(handler, npc.RowId);
                AddInclusionShop(handler, npc.RowId);
            }
        }
    }

    private void AddCustomTalk(uint handler, uint npcId, uint firstSpecial, uint lastSpecial)
    {
        if (!data.GetExcelSheet<CustomTalk>()!.TryGetRow(handler, out var talk))
            return;

        foreach (var script in talk.Script)
        {
            if (script.ScriptArg >= firstSpecial && script.ScriptArg <= lastSpecial)
                Link(script.ScriptArg, npcId);
        }

        if (talk.SpecialLinks.RowId != 0
            && data.GetSubrowExcelSheet<CustomTalkNestHandlers>()!.TryGetRow(talk.SpecialLinks.RowId, out var nest))
        {
            foreach (var link in nest)
                Link(link.NestHandler.RowId, npcId);
        }
    }

    private void AddTopicSelect(uint handler, uint npcId)
    {
        if (!data.GetExcelSheet<TopicSelect>()!.TryGetRow(handler, out var topic))
            return;

        foreach (var shopRef in topic.Shop)
        {
            var shop = shopRef.RowId;
            if (shop == 0)
                continue;
            if (IsType(shop, SpecialShopType) || IsType(shop, GilShopType))
                Link(shop, npcId);
            else
                AddPreHandler(shop, npcId);
        }
    }

    private void AddPreHandler(uint handler, uint npcId)
    {
        if (!data.GetExcelSheet<PreHandler>()!.TryGetRow(handler, out var pre) || pre.Target.RowId == 0)
            return;

        if (IsType(pre.Target.RowId, SpecialShopType))
            Link(pre.Target.RowId, npcId);
        else
            AddInclusionShop(pre.Target.RowId, npcId);
    }

    private void AddInclusionShop(uint handler, uint npcId)
    {
        if (!data.GetExcelSheet<InclusionShop>()!.TryGetRow(handler, out var inclusion))
            return;

        var seriesSheet = data.GetSubrowExcelSheet<InclusionShopSeries>()!;
        foreach (var category in inclusion.Category)
        {
            if (category.RowId == 0 || !category.IsValid)
                continue;
            if (!seriesSheet.TryGetRow(category.Value.InclusionShopSeries.RowId, out var series))
                continue;
            foreach (var entry in series)
                Link(entry.SpecialShop.RowId, npcId);
        }
    }

    // FateShop rows use the NPC's ENpcBase id as their own row id.
    private void MapFateShops()
    {
        foreach (var fateShop in data.GetExcelSheet<FateShop>()!)
        {
            foreach (var shop in fateShop.SpecialShop)
                Link(shop.RowId, fateShop.RowId);
        }
    }

    private void MapSupplementalShops(Action<string> log)
    {
        var rows = CsvLoader.LoadResource<ENpcShop>(CsvLoader.ENpcShopResourceName, true, out var failed, out _, data, null);
        foreach (var row in rows)
            Link(row.ShopId, row.ENpcResidentId);
        if (failed.Count > 0)
            log($"ENpcShop: {failed.Count} lines skipped");
    }

    // Level rows of type 8 place event NPCs in the world.
    private void LoadLevelPlaces()
    {
        const byte EventNpc = 8;
        foreach (var level in data.GetExcelSheet<Level>()!)
        {
            var npcId = level.Object.RowId;
            if (level.Type != EventNpc || npcId == 0 || places.ContainsKey(npcId) || !level.Map.IsValid || level.Territory.RowId == 0)
                continue;

            var map = level.Map.Value;
            var position = new Vector2(
                MapCoordinates.FromWorld(level.X, map.SizeFactor, map.OffsetX),
                MapCoordinates.FromWorld(level.Z, map.SizeFactor, map.OffsetY));
            var name = map.PlaceName.IsValid ? map.PlaceName.Value.Name.ExtractText() : "";
            places[npcId] = new NpcPlace(level.Territory.RowId, map.RowId, position, name);
        }
    }

    private void LoadSupplementalPlaces(Action<string> log)
    {
        var placeNames = data.GetExcelSheet<PlaceName>()!;
        var rows = CsvLoader.LoadResource<ENpcPlace>(CsvLoader.ENpcPlaceResourceName, true, out var failed, out _, data, null);
        int compared = 0;
        float deviation = 0;
        foreach (var row in rows)
        {
            if (places.TryGetValue(row.ENpcResidentId, out var known))
            {
                // Diagnostics: both sources should agree where they overlap.
                if (known.TerritoryId == row.TerritoryTypeId)
                {
                    compared++;
                    deviation += Vector2.Distance(known.MapPosition, row.Position);
                }
                continue;
            }
            var name = placeNames.TryGetRow(row.PlaceNameId, out var place) ? place.Name.ExtractText() : "";
            places[row.ENpcResidentId] = new NpcPlace(row.TerritoryTypeId, row.MapId, row.Position, name);
        }
        if (failed.Count > 0)
            log($"ENpcPlace: {failed.Count} lines skipped");
        if (compared > 0)
            log($"NPC places: Level vs ENpcPlace compared {compared}, mean deviation {deviation / compared:0.00} map units");
    }
}
