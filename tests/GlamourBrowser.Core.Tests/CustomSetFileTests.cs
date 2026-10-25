using GlamourBrowser.Core.CustomSets;
using static GlamourBrowser.Core.Items.EquipSlot;

namespace GlamourBrowser.Core.Tests;

public sealed class CustomSetFileTests : IDisposable
{
    private static readonly DateTimeOffset Now = new(2026, 10, 25, 15, 0, 0, TimeSpan.FromHours(1));

    private readonly string dir = Path.Combine(Path.GetTempPath(), "gb-tests-" + Guid.NewGuid().ToString("N"));
    private string FilePath => Path.Combine(dir, "123.json");

    public CustomSetFileTests() => Directory.CreateDirectory(dir);

    public void Dispose() => Directory.Delete(dir, true);

    [Fact]
    public void SaveAndLoad_RoundTrips()
    {
        var store = new CustomSetStore(123);
        store.Add("Rot", [new CustomPiece(Body, 10, 7, 9), new CustomPiece(RingLeft, 30, 0, 0)], Now);
        store.Add("Blau", [new CustomPiece(Head, 20, 1, 0)], Now.AddMinutes(1));
        CustomSetFile.Save(store, FilePath);

        var loaded = CustomSetFile.Load(FilePath, 123);
        Assert.Equal(123ul, loaded.ContentId);
        Assert.Equal(["Blau", "Rot"], loaded.Sets.Select(s => s.Name));
        Assert.Equal(store.Find(1)!.Pieces, loaded.Find(1)!.Pieces);
        Assert.Equal(Now, loaded.Find(1)!.Changed);
        Assert.False(File.Exists(FilePath + ".tmp"));
    }

    [Fact]
    public void MissingFile_IsEmpty()
    {
        var loaded = CustomSetFile.Load(FilePath, 123);
        Assert.Empty(loaded.Sets);
        Assert.Equal(123ul, loaded.ContentId);
    }

    [Theory]
    [InlineData("{kaputt")]
    [InlineData("""{"ContentId":123}""")]
    [InlineData("""{"ContentId":123,"Sets":[null]}""")]
    [InlineData("""{"ContentId":123,"Sets":[{"Id":1,"Name":"A","Changed":"2026-10-25T15:00:00+01:00"}]}""")]
    [InlineData("""{"ContentId":123,"Sets":[{"Id":1,"Name":"A","Changed":"2026-10-25T15:00:00+01:00","Pieces":[{"Slot":5,"ItemId":10,"Stain0":0,"Stain1":0}]}]}""")]
    [InlineData("""{"ContentId":123,"Sets":[{"Id":1,"Name":"A","Changed":"2026-10-25T15:00:00+01:00","Pieces":[{"Slot":99,"ItemId":10,"Stain0":0,"Stain1":0}]}]}""")]
    public void BrokenFile_MovesToBak(string content)
    {
        File.WriteAllText(FilePath, content);
        var loaded = CustomSetFile.Load(FilePath, 123);

        Assert.Empty(loaded.Sets);
        Assert.False(File.Exists(FilePath));
        Assert.Equal(content, File.ReadAllText(FilePath + ".bak"));
    }

    [Theory]
    [InlineData("""{"ContentId":123,"NextId":4,"Sets":[{"Id":7,"Name":"A""", 8u)]
    [InlineData("""{"ContentId":123,"NextId":9,"Sets":[null]}""", 9u)]
    [InlineData("{kaputt", 1u)]
    public void BrokenFile_NewSetsGetNoIdItHeld(string content, uint firstNewId)
    {
        // A wish may still point at a set of the broken file; a new set must not take its id.
        File.WriteAllText(FilePath, content);
        var loaded = CustomSetFile.Load(FilePath, 123);
        Assert.Equal(firstNewId, loaded.Add("Neu", [new CustomPiece(Body, 10, 0, 0)], Now).Id);
    }

    [Fact]
    public void ForeignContentId_MovesToBak()
    {
        var foreign = new CustomSetStore(999);
        foreign.Add("Fremd", [new CustomPiece(Body, 10, 0, 0)], Now);
        CustomSetFile.Save(foreign, FilePath);

        Assert.Empty(CustomSetFile.Load(FilePath, 123).Sets);
        Assert.True(File.Exists(FilePath + ".bak"));
    }

    [Fact]
    public void UnknownItems_AreKept()
    {
        var store = new CustomSetStore(123);
        store.Add("Alt", [new CustomPiece(Body, 4_000_000, 0, 0)], Now);
        CustomSetFile.Save(store, FilePath);
        Assert.Equal(4_000_000u, Assert.Single(CustomSetFile.Load(FilePath, 123).Find(1)!.Pieces).ItemId);
    }

    [Fact]
    public void Load_KeepsIdsAndNextIdContinues()
    {
        var store = new CustomSetStore(123);
        store.Add("Eins", [new CustomPiece(Body, 10, 0, 0)], Now);
        store.Add("Zwei", [new CustomPiece(Body, 11, 0, 0)], Now);
        store.Remove(2);
        CustomSetFile.Save(store, FilePath);

        // The counter survives the file: the deleted 2 is not given out again.
        var loaded = CustomSetFile.Load(FilePath, 123);
        Assert.Equal([1u], loaded.Sets.Select(s => s.Id));
        Assert.Equal(3u, loaded.Add("Drei", [new CustomPiece(Head, 20, 0, 0)], Now).Id);
    }
}
