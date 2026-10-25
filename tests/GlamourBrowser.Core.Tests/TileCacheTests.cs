using GlamourBrowser.Core.Rendering;

namespace GlamourBrowser.Core.Tests;

public class TileCacheTests
{
    private sealed class FakeTexture(long size = 0) : IDisposable
    {
        public long Size { get; } = size;
        public bool Disposed { get; private set; }
        public void Dispose() => Disposed = true;
    }

    [Fact]
    public void Sum_AddsUpTheCachedTextures()
    {
        using var cache = new TileCache<FakeTexture>();
        cache.Add(Key(1), new FakeTexture(100));
        cache.Add(Key(2), new FakeTexture(23));

        Assert.Equal(123, cache.Sum(t => t.Size));
    }

    private static TileKey Key(uint id) => new(id, 0, 0, TileMode.CurrentOutfit, 1, 192, TileKey.StandardPose);

    [Fact]
    public void Add_SameKeyDisposesOld()
    {
        using var cache = new TileCache<FakeTexture>();
        var old = new FakeTexture();
        cache.Add(Key(1), old);
        cache.Add(Key(1), new FakeTexture());

        Assert.True(old.Disposed);
        Assert.Equal(1, cache.Count);
    }

    [Fact]
    public void Retain_DisposesOutsideSet()
    {
        using var cache = new TileCache<FakeTexture>();
        var keep = new FakeTexture();
        var drop = new FakeTexture();
        cache.Add(Key(1), keep);
        cache.Add(Key(2), drop);

        cache.Retain(new HashSet<TileKey> { Key(1) });

        Assert.False(keep.Disposed);
        Assert.True(drop.Disposed);
        Assert.False(cache.TryGet(Key(2), out _));
        Assert.True(cache.TryGet(Key(1), out var got));
        Assert.Same(keep, got);
    }

    [Fact]
    public void Add_AfterRetain_OutsideSetIsDisposedImmediately()
    {
        using var cache = new TileCache<FakeTexture>();
        cache.Retain(new HashSet<TileKey> { Key(1) });
        var stale = new FakeTexture();
        cache.Add(Key(9), stale);

        Assert.True(stale.Disposed);
        Assert.Equal(0, cache.Count);
    }

    [Fact]
    public void Clear_DisposesAll()
    {
        var cache = new TileCache<FakeTexture>();
        var a = new FakeTexture();
        cache.Add(Key(1), a);
        cache.Clear();

        Assert.True(a.Disposed);
        Assert.Equal(0, cache.Count);
    }

    [Fact]
    public void TileKey_PoseIsPartOfKey()
    {
        var standard = new TileKey(1, 0, 0, TileMode.CurrentOutfit, 1, 192, TileKey.StandardPose);
        var idle4 = standard with { Pose = 4 };
        Assert.NotEqual(standard, idle4);
        Assert.Equal(idle4, new TileKey(1, 0, 0, TileMode.CurrentOutfit, 1, 192, 4));
    }
}
