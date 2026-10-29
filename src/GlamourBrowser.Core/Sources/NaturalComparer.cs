using GlamourBrowser.Core.Localization;

namespace GlamourBrowser.Core.Sources;

/// <summary>
/// Compares names as people count: numbers in them by their value ("Set 3" before "Set 13"), the text between them in
/// the language's alphabet, ignoring case (Ä with A).
/// </summary>
public sealed class NaturalComparer(StringComparer text) : IComparer<string>
{
    /// <summary>In the language loaded now (<see cref="Loc.Culture"/>).</summary>
    public static NaturalComparer ForLanguage() => new(StringComparer.Create(Loc.Culture, ignoreCase: true));

    public int Compare(string? x, string? y)
    {
        if (x == null || y == null)
            return x == null ? y == null ? 0 : -1 : 1;

        var (i, j) = (0, 0);
        while (i < x.Length && j < y.Length)
        {
            var (a, b) = (Run(x, ref i), Run(y, ref j));
            var compared = char.IsAsciiDigit(a[0]) && char.IsAsciiDigit(b[0]) ? CompareNumbers(a, b) : text.Compare(a, b);
            if (compared != 0)
                return compared;
        }
        // The shorter one first ("Set" before "Set 2"); equal numbers written otherwise ("02", "2") by the whole text.
        return i < x.Length ? 1 : j < y.Length ? -1 : text.Compare(x, y);
    }

    /// <summary>The digits or the other characters from <paramref name="at"/> on, whichever comes there.</summary>
    private static string Run(string s, ref int at)
    {
        var start = at;
        var digits = char.IsAsciiDigit(s[at]);
        while (at < s.Length && char.IsAsciiDigit(s[at]) == digits)
            at++;
        return s[start..at];
    }

    /// <summary>By value, of any length: without leading zeros the longer number is the larger one.</summary>
    private static int CompareNumbers(string a, string b)
    {
        var (x, y) = (a.TrimStart('0'), b.TrimStart('0'));
        return x.Length != y.Length ? x.Length.CompareTo(y.Length) : string.CompareOrdinal(x, y);
    }
}
