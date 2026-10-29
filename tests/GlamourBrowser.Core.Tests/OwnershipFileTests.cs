using GlamourBrowser.Core.Ownership;

namespace GlamourBrowser.Core.Tests;

public sealed class OwnershipFileTests : IDisposable
{
    private readonly string dir = Path.Combine(Path.GetTempPath(), "gb-tests-" + Guid.NewGuid().ToString("N"));
    private string FilePath => Path.Combine(dir, "123.json");

    public OwnershipFileTests() => Directory.CreateDirectory(dir);

    public void Dispose() => Directory.Delete(dir, true);

    [Fact]
    public void SaveAndLoad_RoundTrips()
    {
        var seen = new DateTimeOffset(2026, 10, 24, 13, 40, 12, TimeSpan.FromHours(2));
        var store = new OwnershipStore(123);
        store.ReplaceContainer(new ContainerSnapshot(new ContainerId(ContainerKind.Retainer, "42"), "Mira", seen, [10, 11]));
        OwnershipFile.Save(store, FilePath);

        var loaded = OwnershipFile.Load(FilePath, 123);
        var c = Assert.Single(loaded.Containers);
        Assert.Equal(new ContainerId(ContainerKind.Retainer, "42"), c.Id);
        Assert.Equal("Mira", c.Name);
        Assert.Equal("Gehilfe Mira", c.DisplayName);
        Assert.Equal(seen.ToUnixTimeSeconds(), c.Seen.ToUnixTimeSeconds());
        Assert.Equal([10u, 11u], c.ItemIds);
    }

    [Fact]
    public void SaveAndLoad_KeepsTheDresserEntriesWithTheirDyes()
    {
        var store = new OwnershipStore(123);
        DresserEntry[] entries = [new(10, 5, 7), new(1_000_012, 0, 0)];
        store.ReplaceContainer(new ContainerSnapshot(new ContainerId(ContainerKind.Dresser, ""), "", DateTimeOffset.Now, [10, 12]) { Entries = entries });
        store.ReplaceContainer(new ContainerSnapshot(new ContainerId(ContainerKind.Inventory, ""), "", DateTimeOffset.Now, [40]));
        OwnershipFile.Save(store, FilePath);

        var loaded = OwnershipFile.Load(FilePath, 123).Containers.ToDictionary(c => c.Id.Kind);
        Assert.Equal(entries, loaded[ContainerKind.Dresser].Entries);
        Assert.Empty(loaded[ContainerKind.Inventory].Entries);
    }

    [Fact]
    public void Load_OlderFile_KeepsOnlyTheRetainersOwnName()
    {
        File.WriteAllText(FilePath, """
            {"ContentId":123,"Containers":[
              {"Kind":5,"Key":"42","DisplayName":"Retainer Mira","Seen":"2026-10-24T13:40:12+02:00","ItemIds":[10]},
              {"Kind":6,"Key":"","DisplayName":"Kommode","Seen":"2026-10-24T13:40:12+02:00","ItemIds":[11]}]}
            """);
        var loaded = OwnershipFile.Load(FilePath, 123);

        var byKind = loaded.Containers.ToDictionary(c => c.Id.Kind, c => c.Name);
        Assert.Equal("Mira", byKind[ContainerKind.Retainer]);
        Assert.Equal("", byKind[ContainerKind.Dresser]);
        // Without dresser entries (read before they were kept).
        Assert.All(loaded.Containers, c => Assert.Empty(c.Entries));
    }

    [Fact]
    public void Load_MissingFile_ReturnsEmpty()
    {
        var loaded = OwnershipFile.Load(FilePath, 123);
        Assert.Empty(loaded.Containers);
        Assert.Equal(123ul, loaded.ContentId);
    }

    [Fact]
    public void Load_CorruptFile_MovesToBakAndReturnsEmpty()
    {
        File.WriteAllText(FilePath, "{kaputt");
        var loaded = OwnershipFile.Load(FilePath, 123);

        Assert.Empty(loaded.Containers);
        Assert.False(File.Exists(FilePath));
        Assert.Equal("{kaputt", File.ReadAllText(FilePath + ".bak"));
    }

    [Fact]
    public void Save_LeavesNoTmpFile()
    {
        OwnershipFile.Save(new OwnershipStore(123), FilePath);
        Assert.True(File.Exists(FilePath));
        Assert.False(File.Exists(FilePath + ".tmp"));
    }
}
