using GlamourBrowser.Core.Sources;

namespace GlamourBrowser.Core.Ui;

/// <summary>Text and icon layout under a tile's name.</summary>
public static class TileInfo
{
    public static string Line(int level, SourceCategory? top)
        => top is { } category ? $"Lv. {level} · {SourceCategories.DisplayName(category)}" : $"Lv. {level}";

    /// <summary>For owned items: where the item is (first place, plus how many more).</summary>
    public static string OwnedLine(int level, IReadOnlyList<string> places) => places.Count switch
    {
        0 => $"Lv. {level}",
        1 => $"Lv. {level} · {places[0]}",
        _ => $"Lv. {level} · {places[0]} +{places.Count - 1}",
    };

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
