namespace GlamourBrowser.Core.Rendering;

/// <summary>
/// CharaView slots used for tile rendering. 7, 6 and 5 are free in the game but for the party portraits; slot 7 goes to
/// the live preview while it is reserved. 0–4 belong to game windows (0 character, 1 inspection and adventurer plate,
/// 2 try-on, 3 dyes, 4 portrait) and are used only while none of those windows is open, the rarely opened first.
/// </summary>
public static class RenderSlots
{
    public const uint LiveSlot = 7;

    /// <summary>The most views that render at once: seven of the eight slots (the live preview's 7 while it is open, else 0).</summary>
    public const int MaxViews = 7;

    private static readonly uint[] Own = [7, 6, 5];
    private static readonly uint[] Shared = [4, 3, 1, 2, 0];

    /// <param name="sharedFree">No game window that renders a character is open: slots 0–4 may be used too.</param>
    public static IReadOnlyList<uint> Select(int maxViews, bool liveReserved, bool sharedFree = false)
        => Own.Concat(sharedFree ? Shared : [])
              .Where(s => !(liveReserved && s == LiveSlot))
              .Take(Math.Clamp(maxViews, 1, MaxViews))
              .ToList();

    /// <summary>A slot of a game window (0–4), given up as soon as such a window opens.</summary>
    public static bool IsShared(uint slot) => slot < 5;
}
