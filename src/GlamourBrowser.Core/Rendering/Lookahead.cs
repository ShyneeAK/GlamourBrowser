namespace GlamourBrowser.Core.Rendering;

/// <summary>
/// The pages rendered ahead of the current one (setting "Seiten vorausrendern"), in the paging direction, the nearest
/// first. Where that direction ends (the last page, or the first when paging back) the pages on the other side follow.
/// </summary>
public static class Lookahead
{
    /// <summary>The most pages rendered ahead.</summary>
    public const int MaxPages = 10;

    /// <param name="backward">Paging towards the first page: the pages before the current one come first.</param>
    public static IReadOnlyList<int> Pages(int pageIndex, int pageCount, int count, bool backward = false)
    {
        count = Math.Max(0, count);
        var after = Enumerable.Range(pageIndex + 1, Math.Max(0, pageCount - 1 - pageIndex));
        var before = Enumerable.Range(0, Math.Clamp(pageIndex, 0, pageCount)).Reverse();
        var (first, second) = backward ? (before, after) : (after, before);
        return first.Concat(second).Take(count).ToList();
    }
}
