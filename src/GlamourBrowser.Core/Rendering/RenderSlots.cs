namespace GlamourBrowser.Core.Rendering;

/// <summary>CharaView slots used for tile rendering. Slot 7 goes to the live preview while it is reserved.</summary>
public static class RenderSlots
{
    public const uint LiveSlot = 7;

    private static readonly uint[] All = [7, 6, 5];

    public static IReadOnlyList<uint> Select(int maxViews, bool liveReserved)
        => All.Where(s => !(liveReserved && s == LiveSlot)).Take(Math.Clamp(maxViews, 1, All.Length)).ToList();
}
