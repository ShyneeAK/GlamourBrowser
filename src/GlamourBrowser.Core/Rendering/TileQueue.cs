namespace GlamourBrowser.Core.Rendering;

public enum TilePriority
{
    CurrentPage = 0,
    NextPage = 1,
}

/// <summary>Pending tile renders; the current page is served before the next one.</summary>
public sealed class TileQueue
{
    private readonly Queue<TileKey> current = new();
    private readonly Queue<TileKey> next = new();

    public int Count => current.Count + next.Count;

    /// <summary>Replaces all pending requests.</summary>
    public void SetRequests(IEnumerable<TileKey> currentPage, IEnumerable<TileKey> nextPage)
    {
        Clear();
        foreach (var key in currentPage)
            current.Enqueue(key);
        foreach (var key in nextPage)
            next.Enqueue(key);
    }

    public bool TryDequeue(out TileKey key, out TilePriority priority)
    {
        if (current.TryDequeue(out key))
        {
            priority = TilePriority.CurrentPage;
            return true;
        }

        priority = TilePriority.NextPage;
        return next.TryDequeue(out key);
    }

    public void Clear()
    {
        current.Clear();
        next.Clear();
    }
}
