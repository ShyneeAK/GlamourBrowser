namespace GlamourBrowser.Core.Sets;

/// <summary>Names of sets without an own name, built from the names of their pieces.</summary>
public static class SetNames
{
    private static readonly char[] Separators = ['-', ' '];

    /// <summary>
    /// The start all names share, up to and including its last hyphen or space ("Asuran-"); empty when there is
    /// none or the part before it is shorter than 3 characters. Old item markers ("†") are ignored.
    /// </summary>
    public static string CommonStart(IReadOnlyList<string> names)
    {
        if (names.Count == 0)
            return "";
        var clean = names.Select(Clean).ToList();
        var start = clean.Skip(1).Aggregate(clean[0], (common, name) => common[..SharedLength(common, name)]);
        var cut = start.LastIndexOfAny(Separators);
        return cut >= 3 ? start[..(cut + 1)] : "";
    }

    /// <summary>
    /// "Asuran-Serie des Spähens": the common start without its separator, "-Serie", and the common ending from
    /// " des "/" der " on. Null when the names share no start (German adjective endings often prevent one).
    /// </summary>
    public static string? FromCommonStart(IReadOnlyList<string> names)
    {
        var start = CommonStart(names);
        return start.Length == 0 ? null : start[..^1] + "-Serie" + CommonEnding(names.Select(Clean).ToList());
    }

    public static string Fallback(string pieceName) => $"{Clean(pieceName)} (Serie)";

    private static string CommonEnding(IReadOnlyList<string> names)
    {
        var ending = names.Skip(1).Aggregate(names[0], (common, name) => common[^SharedEndLength(common, name)..]);
        foreach (var article in new[] { " des ", " der " })
        {
            var at = ending.IndexOf(article, StringComparison.Ordinal);
            if (at >= 0)
                return ending[at..];
        }
        return "";
    }

    private static int SharedLength(string a, string b)
    {
        var length = 0;
        while (length < a.Length && length < b.Length && a[length] == b[length])
            length++;
        return length;
    }

    private static int SharedEndLength(string a, string b)
    {
        var length = 0;
        while (length < a.Length && length < b.Length && a[^(length + 1)] == b[^(length + 1)])
            length++;
        return length;
    }

    private static string Clean(string name) => name.Replace("†", "").Trim();
}
