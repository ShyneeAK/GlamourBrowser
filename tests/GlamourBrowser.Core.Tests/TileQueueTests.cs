using GlamourBrowser.Core.Rendering;

namespace GlamourBrowser.Core.Tests;

public class TileQueueTests
{
    private static TileKey Key(uint id) => new(id, 0, 0, TileMode.CurrentOutfit, 1, 192, TileKey.StandardPose, 0);

    [Fact]
    public void Dequeue_CurrentPageBeforeNextPage()
    {
        var queue = new TileQueue();
        queue.SetRequests([Key(1), Key(2)], [Key(3)]);

        Assert.True(queue.TryDequeue(out var a, out var pa));
        Assert.True(queue.TryDequeue(out var b, out _));
        Assert.True(queue.TryDequeue(out var c, out var pc));
        Assert.Equal([1u, 2u, 3u], new[] { a.ItemId, b.ItemId, c.ItemId });
        Assert.Equal(TilePriority.CurrentPage, pa);
        Assert.Equal(TilePriority.NextPage, pc);
    }

    [Fact]
    public void Dequeue_LookaheadKeepsPageOrder()
    {
        var queue = new TileQueue();
        queue.SetRequests([Key(1)], [Key(2), Key(3), Key(4), Key(5)]);

        var order = new List<uint>();
        var priorities = new List<TilePriority>();
        while (queue.TryDequeue(out var key, out var priority))
        {
            order.Add(key.ItemId);
            priorities.Add(priority);
        }
        Assert.Equal([1u, 2u, 3u, 4u, 5u], order);
        Assert.Equal(TilePriority.CurrentPage, priorities[0]);
        Assert.All(priorities.Skip(1), p => Assert.Equal(TilePriority.NextPage, p));
    }

    [Fact]
    public void SetRequests_DropsPreviousPending()
    {
        var queue = new TileQueue();
        queue.SetRequests([Key(1), Key(2)], []);
        queue.SetRequests([Key(5)], []);

        Assert.Equal(1, queue.Count);
        Assert.True(queue.TryDequeue(out var key, out _));
        Assert.Equal(5u, key.ItemId);
    }

    [Fact]
    public void Dequeue_EmptyReturnsFalse()
    {
        Assert.False(new TileQueue().TryDequeue(out _, out _));
    }
}
