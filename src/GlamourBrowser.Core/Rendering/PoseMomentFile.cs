using System.Text.Json;

namespace GlamourBrowser.Core.Rendering;

/// <summary>One character's chosen pose moments (seconds into the animation per stance) as JSON, like <see cref="CalibrationFile"/>.</summary>
public static class PoseMomentFile
{
    private static readonly JsonSerializerOptions Options = new() { WriteIndented = true };

    public static Dictionary<Stance, float> Load(string path)
    {
        if (!File.Exists(path))
            return new Dictionary<Stance, float>();

        try
        {
            var entries = JsonSerializer.Deserialize<List<Entry>>(File.ReadAllText(path), Options)
                          ?? throw new JsonException("empty document");
            return entries.ToDictionary(e => new Stance(e.WeaponDrawn, e.Pose), e => e.Seconds);
        }
        catch (Exception e) when (e is JsonException or NotSupportedException or ArgumentException)
        {
            File.Move(path, path + ".bak", overwrite: true);
            return new Dictionary<Stance, float>();
        }
    }

    /// <summary>Writes to a temporary file first so a concurrent reader never sees half a file.</summary>
    public static void Save(string path, IReadOnlyDictionary<Stance, float> moments)
    {
        var entries = moments.Select(kv => new Entry(kv.Key.WeaponDrawn, kv.Key.Pose, kv.Value)).ToList();
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        var tmp = path + ".tmp";
        File.WriteAllText(tmp, JsonSerializer.Serialize(entries, Options));
        File.Move(tmp, path, overwrite: true);
    }

    private sealed record Entry(bool WeaponDrawn, byte Pose, float Seconds);
}
