using System;
using Dalamud.Game.Text.SeStringHandling.Payloads;
using Dalamud.Interface;
using Dalamud.Utility;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using GlamourBrowser.Core.Localization;
using GlamourBrowser.Core.Sources;

namespace GlamourBrowser.UI;

/// <summary>The button a source gets (map, duty finder, recipe, mog station) and what it does; used by the item card and the plan by source.</summary>
public static unsafe class SourceActions
{
    private const uint MapIcon = 60561;        // map flag
    private const uint DutyFinderIcon = 46;    // main menu: duty finder
    private const uint RecipeIcon = 22;        // main menu: crafting log

    /// <returns>Null when the source has no action.</returns>
    public static (uint Icon, FontAwesomeIcon Fallback, string Tooltip)? ButtonFor(SourceAction? action) => action switch
    {
        MapFlag => (MapIcon, FontAwesomeIcon.MapMarkerAlt, Loc.T("Karte")),
        DutyFinder => (DutyFinderIcon, FontAwesomeIcon.Search, Loc.T("Inhaltssuche")),
        OpenRecipe => (RecipeIcon, FontAwesomeIcon.Book, Loc.T("Rezept")),
        OpenStore => (0u, FontAwesomeIcon.ShoppingCart, Loc.T("Mogstation")),
        _ => null,
    };

    public static void Run(SourceAction action)
    {
        Plugin.Framework.RunOnFrameworkThread(() =>
        {
            try
            {
                // Agents are null-checked: a null agent would crash the game, the catch does not catch access violations.
                switch (action)
                {
                    case MapFlag flag:
                        Plugin.GameGui.OpenMapWithMapLink(new MapLinkPayload(flag.TerritoryId, flag.MapId, flag.X, flag.Y, 0.05f));
                        break;
                    case DutyFinder duty:
                        var finder = AgentContentsFinder.Instance();
                        if (finder != null)
                            finder->OpenRegularDuty(duty.ContentFinderConditionId);
                        else
                            Plugin.Log.Warning("Duty finder agent not available");
                        break;
                    case OpenStore store:
                        Util.OpenLink(StoreLinks.ProductUrl(store.StoreId));
                        break;
                    case OpenRecipe recipe:
                        var recipeNote = AgentRecipeNote.Instance();
                        if (recipeNote != null)
                            recipeNote->OpenRecipeByRecipeId(recipe.RecipeId);
                        else
                            Plugin.Log.Warning("Recipe note agent not available");
                        break;
                }
            }
            catch (Exception e)
            {
                Plugin.Log.Warning(e, $"Source action {action} failed");
            }
        });
    }
}
