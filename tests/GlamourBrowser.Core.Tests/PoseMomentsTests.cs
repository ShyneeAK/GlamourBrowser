using GlamourBrowser.Core.Rendering;

namespace GlamourBrowser.Core.Tests;

public sealed class PoseMomentsTests : IDisposable
{
    private readonly string dir = Path.Combine(Path.GetTempPath(), "gb-moment-" + Guid.NewGuid().ToString("N"));
    private string FilePath => Path.Combine(dir, "1.json");

    public void Dispose()
    {
        if (Directory.Exists(dir))
            Directory.Delete(dir, true);
    }

    [Fact]
    public void SaveAndLoad_RoundTrips()
    {
        var moments = new Dictionary<Stance, float> { [new Stance(false, 4)] = 1.25f, [new Stance(true, 0)] = 0.5f };
        PoseMomentFile.Save(FilePath, moments);
        Assert.Equal(moments, PoseMomentFile.Load(FilePath));
        Assert.False(File.Exists(FilePath + ".tmp"));
    }

    [Fact]
    public void Load_MissingFile_ReturnsEmpty() => Assert.Empty(PoseMomentFile.Load(FilePath));

    [Fact]
    public void Load_CorruptFile_MovesToBakAndReturnsEmpty()
    {
        Directory.CreateDirectory(dir);
        File.WriteAllText(FilePath, "{ not json");
        Assert.Empty(PoseMomentFile.Load(FilePath));
        Assert.True(File.Exists(FilePath + ".bak"));
    }

    [Fact]
    public void TileKey_MomentIsPartOfKey()
    {
        var a = new TileKey(1, 0, 0, TileMode.CurrentOutfit, 1, 192, 4, 0);
        Assert.NotEqual(a, a with { MomentMs = 1250 });
    }

    [Fact]
    public void MomentMs_ClampsAndRounds()
    {
        Assert.Equal(1250, Stance.ToMs(1.2504f));
        Assert.Equal(0, Stance.ToMs(-1f));
        Assert.Equal(ushort.MaxValue, Stance.ToMs(100f));
    }
}
