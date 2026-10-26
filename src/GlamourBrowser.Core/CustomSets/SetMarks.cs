namespace GlamourBrowser.Core.CustomSets;

/// <summary>
/// The custom sets marked in "Meine Sets" to move several at once: Ctrl+click marks one or takes its mark off,
/// Shift+click marks the range from the last clicked one.
/// </summary>
public sealed class SetMarks
{
    private readonly HashSet<uint> marked = new();
    private uint? anchor;

    public IReadOnlySet<uint> Marked => marked;

    public int Count => marked.Count;

    public bool IsMarked(uint setId) => marked.Contains(setId);

    public void Toggle(uint setId)
    {
        if (!marked.Remove(setId))
            marked.Add(setId);
        anchor = setId;
    }

    /// <summary>Marks every set from the last clicked one to <paramref name="setId"/> in the order shown; only it without a last one.</summary>
    /// <param name="shown">The custom set ids in the order the grid shows them.</param>
    public void MarkRange(IReadOnlyList<uint> shown, uint setId)
    {
        var to = IndexOf(shown, setId);
        var from = anchor is { } last ? IndexOf(shown, last) : -1;
        if (to < 0)
            return;
        if (from < 0)
            from = to;
        for (var i = Math.Min(from, to); i <= Math.Max(from, to); i++)
            marked.Add(shown[i]);
        anchor = setId;
    }

    /// <summary>The sets an action on <paramref name="setId"/> applies to: all marked ones when it is marked, else only it.</summary>
    public IReadOnlyList<uint> With(uint setId) => marked.Contains(setId) ? marked.ToList() : [setId];

    public void Clear()
    {
        marked.Clear();
        anchor = null;
    }

    private static int IndexOf(IReadOnlyList<uint> shown, uint id)
    {
        for (var i = 0; i < shown.Count; i++)
        {
            if (shown[i] == id)
                return i;
        }
        return -1;
    }
}
