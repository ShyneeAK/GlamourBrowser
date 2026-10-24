using GlamourBrowser.Core.Items;

namespace GlamourBrowser.Core.Rendering;

/// <summary>Per-slot tile camera. All zero until tuned in game (plan task 11).</summary>
public static class CameraPresets
{
    public static IReadOnlyDictionary<EquipSlot, CameraPreset> Defaults { get; } =
        Enum.GetValues<EquipSlot>().ToDictionary(slot => slot, _ => CameraPreset.Zero);

    public static CameraPreset Resolve(EquipSlot slot, IReadOnlyDictionary<EquipSlot, CameraPreset>? overrides)
        => overrides != null && overrides.TryGetValue(slot, out var preset) ? preset : Defaults[slot];
}
