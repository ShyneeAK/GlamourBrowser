using System.Text.Json;
using GlamourBrowser.Core.Items;

namespace GlamourBrowser.Core.Rendering;

/// <summary>
/// One character's camera calibrations as JSON. Separate files per character keep several game clients
/// (multibox) from overwriting each other's calibrations.
/// </summary>
public static class CalibrationFile
{
    private static readonly JsonSerializerOptions Options = new() { WriteIndented = true, IncludeFields = false };

    public static Dictionary<EquipSlot, CameraPreset> Load(string path)
    {
        if (!File.Exists(path))
            return new Dictionary<EquipSlot, CameraPreset>();

        try
        {
            var entries = JsonSerializer.Deserialize<List<Entry>>(File.ReadAllText(path), Options)
                          ?? throw new JsonException("empty document");
            return entries.ToDictionary(e => e.Slot, e => new CameraPreset(e.Rotation, e.PositionX, e.PositionY, e.Distance));
        }
        catch (Exception e) when (e is JsonException or NotSupportedException or ArgumentException)
        {
            File.Move(path, path + ".bak", overwrite: true);
            return new Dictionary<EquipSlot, CameraPreset>();
        }
    }

    /// <summary>Writes to a temporary file first so a concurrent reader never sees half a file.</summary>
    public static void Save(string path, IReadOnlyDictionary<EquipSlot, CameraPreset> slots)
    {
        var entries = slots.Select(kv => new Entry(kv.Key, kv.Value.Rotation, kv.Value.PositionX, kv.Value.PositionY, kv.Value.Distance)).ToList();
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        var tmp = path + ".tmp";
        File.WriteAllText(tmp, JsonSerializer.Serialize(entries, Options));
        File.Move(tmp, path, overwrite: true);
    }

    private sealed record Entry(EquipSlot Slot, float Rotation, float PositionX, float PositionY, float Distance);
}
