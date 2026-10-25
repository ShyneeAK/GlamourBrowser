using GlamourBrowser.Core.Items;

namespace GlamourBrowser.Core.Rendering;

/// <summary>
/// The job a view's character takes on before drawing a weapon, so its battle stance fits the weapon instead of the
/// player's current job. A job beats its base class (same weapon, and the job is what it is made for).
/// </summary>
public static class StanceJob
{
    /// <param name="jobs">The jobs and classes that can equip the weapon.</param>
    /// <param name="parentOf">A job's base class; a base class (and a job without one) is its own parent.</param>
    /// <returns>The lowest id of a job with a base class, else the lowest id; null when no job can equip it.</returns>
    public static byte? For(IReadOnlySet<uint> jobs, Func<uint, uint> parentOf)
    {
        if (jobs.Count == 0)
            return null;
        var withBase = jobs.Where(j => parentOf(j) != j).ToList();
        return (byte)(withBase.Count > 0 ? withBase.Min() : jobs.Min());
    }

    /// <summary>The job for a main-hand weapon; null for anything else, which keeps the player's own job.</summary>
    public static byte? ForItem(GlamourItem item, Func<uint, uint> parentOf)
        => item.Slot == EquipSlot.MainHand ? For(item.ClassJobIds, parentOf) : null;
}
