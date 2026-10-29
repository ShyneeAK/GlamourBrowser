namespace GlamourBrowser.Core.Items;

/// <summary>
/// The search text: words with a leading minus ("-rock") leave out every name containing them, the rest is searched as
/// one text, as before.
/// </summary>
public sealed record SearchQuery(string Include, IReadOnlyList<string> Excludes)
{
    public static SearchQuery Parse(string text)
    {
        var words = text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
        // A minus alone excludes nothing.
        var excludes = words.Where(w => w.Length > 1 && w[0] == '-').Select(w => w[1..]).ToList();
        var include = string.Join(' ', words.Where(w => w[0] != '-'));
        return new SearchQuery(include, excludes);
    }

    public bool IsEmpty => Include.Length == 0 && Excludes.Count == 0;

    public bool Matches(string name) => Matches([name]);

    /// <summary>
    /// For a set: one of the names (its own or a piece's) contains the searched text, and none contains an excluded
    /// word.
    /// </summary>
    public bool Matches(IEnumerable<string> names)
    {
        var list = names as IReadOnlyCollection<string> ?? names.ToList();
        return (Include.Length == 0 || list.Any(n => n.Contains(Include, StringComparison.OrdinalIgnoreCase)))
               && !list.Any(n => Excludes.Any(e => n.Contains(e, StringComparison.OrdinalIgnoreCase)));
    }
}
