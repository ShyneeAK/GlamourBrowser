namespace GlamourBrowser.Core.Rendering;

/// <summary>The pages rendered ahead of the current one (setting "Seiten vorausrendern"), the next one first.</summary>
public static class Lookahead
{
    /// <summary>The most pages rendered ahead.</summary>
    public const int MaxPages = 10;

    public static IReadOnlyList<int> Pages(int pageIndex, int pageCount, int count)
    {
        var last = Math.Min(pageIndex + Math.Max(0, count), pageCount - 1);
        return last > pageIndex ? Enumerable.Range(pageIndex + 1, last - pageIndex).ToList() : [];
    }
}
