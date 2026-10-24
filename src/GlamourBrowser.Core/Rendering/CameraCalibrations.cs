using GlamourBrowser.Core.Items;

namespace GlamourBrowser.Core.Rendering;

/// <summary>Per-character, per-slot camera calibrations over a dictionary owned by the configuration.</summary>
public sealed class CameraCalibrations(Dictionary<ulong, Dictionary<EquipSlot, CameraPreset>> data)
{
    public CameraPreset? Get(ulong contentId, EquipSlot slot)
        => data.TryGetValue(contentId, out var slots) && slots.TryGetValue(Normalize(slot), out var preset) ? preset : null;

    public void Set(ulong contentId, EquipSlot slot, CameraPreset preset)
    {
        if (!data.TryGetValue(contentId, out var slots))
            data[contentId] = slots = new Dictionary<EquipSlot, CameraPreset>();
        slots[Normalize(slot)] = preset;
    }

    public bool Remove(ulong contentId, EquipSlot slot)
        => data.TryGetValue(contentId, out var slots) && slots.Remove(Normalize(slot));

    public IReadOnlyDictionary<EquipSlot, CameraPreset> For(ulong contentId)
        => data.TryGetValue(contentId, out var slots) ? slots : new Dictionary<EquipSlot, CameraPreset>();

    // Ring items are always shown on the right hand; both ring slots share one calibration.
    private static EquipSlot Normalize(EquipSlot slot) => slot == EquipSlot.RingLeft ? EquipSlot.RingRight : slot;
}
