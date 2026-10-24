using System.Collections.Generic;
using System.IO;
using System.Linq;
using GlamourBrowser.Core.Items;
using GlamourBrowser.Core.Rendering;

namespace GlamourBrowser.Rendering;

/// <summary>
/// Camera calibrations per character, each in its own file under ConfigDirectory/calibration, so that
/// several game clients sharing the plugin config never overwrite each other's calibrations.
/// </summary>
public sealed class CalibrationService
{
    private readonly string directory;
    private readonly Dictionary<ulong, Dictionary<EquipSlot, CameraPreset>> loaded = new();

    public CalibrationService(Configuration config)
    {
        directory = Path.Combine(Plugin.PluginInterface.ConfigDirectory.FullName, "calibration");
        Calibrations = new CameraCalibrations(loaded);
        MigrateFromConfiguration(config);
    }

    /// <summary>Calibrations of characters loaded so far; call <see cref="Ensure"/> before reading one.</summary>
    public CameraCalibrations Calibrations { get; }

    public CameraPreset? Get(ulong contentId, EquipSlot slot)
    {
        Ensure(contentId);
        return Calibrations.Get(contentId, slot);
    }

    public void Ensure(ulong contentId)
    {
        if (contentId != 0 && !loaded.ContainsKey(contentId))
            loaded[contentId] = CalibrationFile.Load(PathFor(contentId));
    }

    public void Set(ulong contentId, EquipSlot slot, CameraPreset preset)
    {
        Ensure(contentId);
        Calibrations.Set(contentId, slot, preset);
        Save(contentId);
    }

    public bool Remove(ulong contentId, EquipSlot slot)
    {
        Ensure(contentId);
        if (!Calibrations.Remove(contentId, slot))
            return false;
        Save(contentId);
        return true;
    }

    private void Save(ulong contentId)
    {
        try
        {
            CalibrationFile.Save(PathFor(contentId), Calibrations.For(contentId));
        }
        catch (IOException e)
        {
            Plugin.Log.Warning(e, $"Saving calibration for {contentId} failed");
        }
    }

    private string PathFor(ulong contentId) => Path.Combine(directory, $"{contentId}.json");

    /// <summary>Earlier versions kept calibrations in the shared configuration; move them to per-character files once.</summary>
    private void MigrateFromConfiguration(Configuration config)
    {
        if (config.CameraCalibrations.Count == 0)
            return;

        foreach (var (contentId, slots) in config.CameraCalibrations.Where(kv => !File.Exists(PathFor(kv.Key))))
            CalibrationFile.Save(PathFor(contentId), slots);
        config.CameraCalibrations.Clear();
        config.Save();
        Plugin.Log.Information("Camera calibrations moved to per-character files");
    }
}
