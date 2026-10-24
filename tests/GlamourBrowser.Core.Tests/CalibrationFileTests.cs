using GlamourBrowser.Core.Items;
using GlamourBrowser.Core.Rendering;

namespace GlamourBrowser.Core.Tests;

public sealed class CalibrationFileTests : IDisposable
{
    private readonly string dir = Path.Combine(Path.GetTempPath(), "gb-cal-" + Guid.NewGuid().ToString("N"));
    private string FilePath => Path.Combine(dir, "calibration", "123.json");

    public void Dispose()
    {
        if (Directory.Exists(dir))
            Directory.Delete(dir, true);
    }

    [Fact]
    public void SaveAndLoad_RoundTrips()
    {
        var slots = new Dictionary<EquipSlot, CameraPreset> { [EquipSlot.Head] = new(0.5f, 0.1f, 1.2f, 2.0f) };
        CalibrationFile.Save(FilePath, slots);

        var loaded = CalibrationFile.Load(FilePath);
        Assert.Equal(slots, loaded);
        Assert.False(File.Exists(FilePath + ".tmp"));
    }

    [Fact]
    public void Load_MissingFile_ReturnsEmpty() => Assert.Empty(CalibrationFile.Load(FilePath));

    [Fact]
    public void Load_CorruptFile_MovesToBakAndReturnsEmpty()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
        File.WriteAllText(FilePath, "[kaputt");

        Assert.Empty(CalibrationFile.Load(FilePath));
        Assert.True(File.Exists(FilePath + ".bak"));
        Assert.False(File.Exists(FilePath));
    }
}
