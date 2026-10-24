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
        store.ReplaceContainer(new ContainerSnapshot(new ContainerId(ContainerKind.Retainer, "42"), "Gehilfe Mira", seen, [10, 11]));
        OwnershipFile.Save(store, FilePath);

        var loaded = OwnershipFile.Load(FilePath, 123);
        var c = Assert.Single(loaded.Containers);
        Assert.Equal(new ContainerId(ContainerKind.Retainer, "42"), c.Id);
        Assert.Equal("Gehilfe Mira", c.DisplayName);
        Assert.Equal(seen.ToUnixTimeSeconds(), c.Seen.ToUnixTimeSeconds());
        Assert.Equal([10u, 11u], c.ItemIds);
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
