using System.Collections.Generic;
using GlamourBrowser.Core.Rendering;
using Lumina.Excel.Sheets;

namespace GlamourBrowser.Rendering;

/// <summary>The wrist each bracelet is on, read from its model the first time its camera is asked for.</summary>
public sealed class WristSides
{
    private readonly Dictionary<uint, WristSide> known = new();

    public WristSide Of(uint itemId)
    {
        if (known.TryGetValue(itemId, out var side))
            return side;
        side = WristSide.Both;
        if (Plugin.DataManager.GetExcelSheet<Item>().GetRowOrDefault(itemId) is { } item)
        {
            // The model set; the side is the same for every race, so the first race model found tells it.
            var set = item.ModelMain & 0xFFFF;
            foreach (var race in new[] { "c0101", "c0201", "c1101", "c1201" })
            {
                if (Plugin.DataManager.GetFile($"chara/accessory/a{set:D4}/model/{race}a{set:D4}_wrs.mdl") is { } file)
                {
                    side = WristModels.SideOf(file.Data);
                    break;
                }
            }
        }
        return known[itemId] = side;
    }
}
