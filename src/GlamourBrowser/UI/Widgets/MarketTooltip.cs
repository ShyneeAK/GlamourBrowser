using System;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility.Raii;
using GlamourBrowser.Core.Localization;
using GlamourBrowser.Market;

namespace GlamourBrowser.UI.Widgets;

/// <summary>A tooltip with the cheapest listing on the home world and on its data center (Universalis), for the market icon of a tile and the card.</summary>
public static class MarketTooltip
{
    /// <param name="hint">A last, muted line (what a click does); none when null.</param>
    public static void Draw(MarketService market, uint itemId, string? hint = null)
    {
        using var tooltip = ImRaii.Tooltip();
        ImGui.TextUnformatted(Loc.T("Marktbrett") + " · Universalis");
        if (!Plugin.PlayerState.IsLoaded || !Plugin.PlayerState.HomeWorld.IsValid)
        {
            ImGui.TextDisabled(Loc.T("Preise erst nach dem Einloggen (Heimatwelt)"));
        }
        else
        {
            var home = Plugin.PlayerState.HomeWorld.Value;
            var world = home.Name.ExtractText();
            var dataCenter = home.DataCenter.Value.Name.ExtractText();
            ImGui.TextUnformatted(world + ": " + Text(market.Get(itemId, world), withWorld: false));
            ImGui.TextUnformatted(Loc.F("{0}, günstigstes", dataCenter) + ": " + Text(market.Get(itemId, dataCenter), withWorld: true));
        }
        if (hint == null)
            return;
        ImGui.Spacing();
        ImGui.TextDisabled(hint);
    }

    private static string Text(MarketAnswer answer, bool withWorld) => answer switch
    {
        { State: MarketState.Loading } => Loc.T("lädt …"),
        { State: MarketState.Failed } => Loc.T("nicht erreichbar"),
        { Price: null } => Loc.T("keine Angebote"),
        { Price: { } p } => Loc.F("{0} Gil", p.PricePerUnit.ToString("N0", Loc.Culture))
                            + (p.Hq ? " HQ" : "")
                            + (withWorld && p.World != null ? " " + Loc.F("auf {0}", p.World) : "")
                            + " (" + Age(p.Updated) + ")",
    };

    private static string Age(DateTimeOffset updated)
    {
        var age = DateTimeOffset.UtcNow - updated;
        if (age.TotalHours < 1)
            return Loc.F("vor {0} Min.", Math.Max(1, (int)age.TotalMinutes));
        if (age.TotalDays < 1)
            return Loc.F("vor {0} Std.", (int)age.TotalHours);
        return Loc.F("vor {0} Tagen", (int)age.TotalDays);
    }
}
