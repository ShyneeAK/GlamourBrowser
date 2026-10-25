using System.Text;
using GlamourBrowser.Core.Items;
using GlamourBrowser.Core.Sources;

namespace GlamourBrowser.Core.Ui;

/// <summary>Text and icon layout under a tile's name.</summary>
public static class TileInfo
{
    public static string Line(int level, SourceCategory? top)
        => top is { } category ? $"Lv. {level} · {SourceCategories.DisplayName(category)}" : $"Lv. {level}";

    public static string DyeText(int channels) => channels == 1 ? "Färbbar (1 Farbkanal)" : $"Färbbar ({channels} Farbkanäle)";

    public static string TradeText(Tradability trade) => trade switch
    {
        Tradability.Marketable => "Marktbrett",
        Tradability.Tradable => "Handelbar",
        _ => "Gebunden",
    };

    /// <summary>
    /// Tooltip of a tile's source icon: the sources of every category of the icon's group the item has, best category
    /// first, each with its detail (boss, chest, place); a long list is cut off after <paramref name="maxEntries"/>.
    /// </summary>
    public static string SourceTooltip(SourceCategory shown, IReadOnlyList<ItemSource> sources, int maxEntries = 6)
    {
        var text = new StringBuilder();
        var listed = 0;
        var skipped = 0;
        foreach (var category in SourceRanking.GroupOf(shown))
        {
            var entries = sources.Where(s => s.Category == category)
                .Select(s => s.Detail.Length > 0 ? $"{s.Title} · {s.Detail}" : s.Title)
                .Distinct()
                .ToList();
            var room = Math.Min(entries.Count, maxEntries - listed);
            skipped += entries.Count - Math.Max(0, room);
            if (room <= 0)
                continue;
            if (text.Length > 0)
                text.Append('\n');
            text.Append(SourceCategories.DisplayName(category));
            foreach (var entry in entries.Take(room))
                text.Append("\n  ").Append(entry);
            listed += room;
        }
        if (skipped > 0)
            text.Append($"\n  … und {skipped} weitere");
        return text.Length > 0 ? text.ToString() : SourceCategories.DisplayName(shown);
    }

    /// <summary>For owned items: where the item is (first place, plus how many more).</summary>
    public static string OwnedLine(int level, IReadOnlyList<string> places) => places.Count switch
    {
        0 => $"Lv. {level}",
        1 => $"Lv. {level} · {places[0]}",
        _ => $"Lv. {level} · {places[0]} +{places.Count - 1}",
    };

    /// <summary>Info rows under the name that fit a tile: icons are dropped first, then line 2, then the name.</summary>
    public static int InfoRows(float tileHeight, float lineHeight)
    {
        if (tileHeight >= lineHeight * 6)
            return 3;
        if (tileHeight >= lineHeight * 4)
            return 2;
        return tileHeight >= lineHeight * 2 ? 1 : 0;
    }

    /// <summary>
    /// An icon row with one right-aligned icon (job/role): the right icon is placed first, the left icons fill the rest.
    /// </summary>
    public static (int Left, bool Right) FitRow(int leftCount, bool hasRight, float iconWidth, float spacing, float available)
    {
        var right = hasRight && iconWidth <= available;
        var left = FitIcons(leftCount, iconWidth, spacing, right ? available - iconWidth - spacing : available);
        return (left, right);
    }

    /// <summary>How many icons of <paramref name="iconWidth"/> fit into <paramref name="available"/>; the rest is cut off on the right.</summary>
    public static int FitIcons(int count, float iconWidth, float spacing, float available)
    {
        var fit = 0;
        var used = 0f;
        while (fit < count)
        {
            var next = used + (fit > 0 ? spacing : 0) + iconWidth;
            if (next > available)
                break;
            used = next;
            fit++;
        }
        return fit;
    }
}
