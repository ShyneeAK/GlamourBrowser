using System.Text;
using GlamourBrowser.Core.Items;
using GlamourBrowser.Core.Localization;
using GlamourBrowser.Core.Sets;
using GlamourBrowser.Core.Sources;

namespace GlamourBrowser.Core.Ui;

/// <summary>Text and icon layout under a tile's name.</summary>
public static class TileInfo
{
    public static string Line(int level, SourceCategory? top)
        => top is { } category ? $"{Level(level)} · {SourceCategories.DisplayName(category)}" : Level(level);

    public static string DyeText(int channels) => channels == 1 ? Loc.T("Färbbar (1 Farbkanal)") : Loc.F("Färbbar ({0} Farbkanäle)", channels);

    public static string TradeText(Tradability trade) => Loc.T(trade switch
    {
        Tradability.Marketable => "Marktbrett",
        Tradability.Tradable => "Handelbar",
        _ => "Gebunden",
    });

    public static string SetLine(int level, SetProgress progress) => $"{Level(level)} · {progress.Owned}/{progress.Total}";

    /// <summary>"Lv. 90" in the loaded language.</summary>
    public static string Level(int level) => Loc.F("Lv. {0}", level);

    /// <summary>
    /// Tooltip of a tile's source icon: the sources of every category of the icon's group the item has, best category
    /// first, each with its detail (boss, chest, place); a long list is cut off after <paramref name="maxEntries"/>.
    /// </summary>
    public static string SourceTooltip(SourceCategory shown, IReadOnlyList<ItemSource> sources, int maxEntries = 6)
        => GroupTooltip(shown, category => sources.Where(s => s.Category == category).Select(Entry).Distinct().ToList(), maxEntries);

    /// <summary>Like <see cref="SourceTooltip"/> for a set: per category one line per piece with its first source there.</summary>
    public static string SetSourceTooltip(SourceCategory shown, IReadOnlyList<(string Piece, IReadOnlyList<ItemSource> Sources)> pieces, int maxEntries = 6)
        => GroupTooltip(shown, category => pieces
            .Select(p => (p.Piece, Source: p.Sources.FirstOrDefault(s => s.Category == category)))
            .Where(p => p.Source != null)
            .Select(p => $"{p.Piece} · {Entry(p.Source!)}")
            .ToList(), maxEntries);

    private static string Entry(ItemSource source) => source.Detail.Length > 0 ? $"{source.Title} · {source.Detail}" : source.Title;

    private static string GroupTooltip(SourceCategory shown, Func<SourceCategory, List<string>> entriesOf, int maxEntries)
    {
        var text = new StringBuilder();
        var listed = 0;
        var skipped = 0;
        foreach (var category in SourceRanking.GroupOf(shown))
        {
            var entries = entriesOf(category);
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
            text.Append("\n  ").Append(Loc.F("… und {0} weitere", skipped));
        return text.Length > 0 ? text.ToString() : SourceCategories.DisplayName(shown);
    }

    /// <summary>For owned items: where the item is (first place, plus how many more).</summary>
    public static string OwnedLine(int level, IReadOnlyList<string> places) => places.Count switch
    {
        0 => Level(level),
        1 => $"{Level(level)} · {places[0]}",
        _ => $"{Level(level)} · {places[0]} +{places.Count - 1}",
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
