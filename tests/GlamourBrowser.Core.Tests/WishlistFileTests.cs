using GlamourBrowser.Core.Sets;
using GlamourBrowser.Core.Wishlist;

namespace GlamourBrowser.Core.Tests;

public sealed class WishlistFileTests : IDisposable
{
    private static readonly DateTimeOffset Now = new(2026, 10, 25, 14, 0, 0, TimeSpan.FromHours(2));

    private readonly string dir = Path.Combine(Path.GetTempPath(), "gb-tests-" + Guid.NewGuid().ToString("N"));
    private string FilePath => Path.Combine(dir, "123.json");

    public WishlistFileTests() => Directory.CreateDirectory(dir);

    public void Dispose() => Directory.Delete(dir, true);

    [Fact]
    public void SaveAndLoad_RoundTrips()
    {
        var store = new WishlistStore(123);
        store.Add(WishKey.ForItem(7), Now);
        store.Add(WishKey.ForSet(new SetId(SetKind.MogStation, 42)), Now.AddMinutes(5));
        WishlistFile.Save(store, FilePath);

        var loaded = WishlistFile.Load(FilePath, 123);
        Assert.Equal(123ul, loaded.ContentId);
        Assert.Equal([new WishEntry(WishKey.ForItem(7), Now), new WishEntry(WishKey.ForSet(new SetId(SetKind.MogStation, 42)), Now.AddMinutes(5))],
            loaded.Entries);
        Assert.False(File.Exists(FilePath + ".tmp"));
    }

    [Fact]
    public void MissingFile_GivesAnEmptyList()
    {
        var loaded = WishlistFile.Load(FilePath, 123);
        Assert.Empty(loaded.Entries);
        Assert.Equal(123ul, loaded.ContentId);
    }

    [Theory]
    [InlineData("{kaputt")]
    [InlineData("""{"ContentId":123}""")]
    public void BrokenFile_MovesToBak_AndStartsEmpty(string content)
    {
        File.WriteAllText(FilePath, content);
        var loaded = WishlistFile.Load(FilePath, 123);

        Assert.Empty(loaded.Entries);
        Assert.False(File.Exists(FilePath));
        Assert.Equal(content, File.ReadAllText(FilePath + ".bak"));
    }

    [Fact]
    public void ForeignContentId_MovesToBak()
    {
        var foreign = new WishlistStore(999);
        foreign.Add(WishKey.ForItem(7), Now);
        WishlistFile.Save(foreign, FilePath);

        var loaded = WishlistFile.Load(FilePath, 123);
        Assert.Empty(loaded.Entries);
        Assert.Equal(123ul, loaded.ContentId);
        Assert.False(File.Exists(FilePath));
        Assert.True(File.Exists(FilePath + ".bak"));
    }

    [Fact]
    public void Load_MapsVariantsToTheirBase_AndMergesDuplicates()
    {
        // Written before 12 became a variant of 10: the base was wished for later than the variant.
        var old = new WishlistStore(123);
        old.Add(WishKey.ForItem(10), Now.AddHours(1));
        old.Add(WishKey.ForItem(12), Now);
        WishlistFile.Save(old, FilePath);

        var loaded = WishlistFile.Load(FilePath, 123, new Dictionary<uint, uint> { [12] = 10 });
        Assert.Equal(new WishEntry(WishKey.ForItem(10), Now), Assert.Single(loaded.Entries));
    }

    [Fact]
    public void UnknownEntries_AreKept()
    {
        // Ids the catalog may not know (any more); the file keeps them, the browser just does not show them.
        var store = new WishlistStore(123);
        store.Add(WishKey.ForItem(4_000_000), Now);
        store.Add(WishKey.ForSet(new SetId(SetKind.Model, 4_000_001)), Now);
        WishlistFile.Save(store, FilePath);
        WishlistFile.Save(WishlistFile.Load(FilePath, 123), FilePath);

        Assert.Equal([WishKey.ForItem(4_000_000), WishKey.ForSet(new SetId(SetKind.Model, 4_000_001))],
            WishlistFile.Load(FilePath, 123).Entries.Select(e => e.Key));
    }
}
