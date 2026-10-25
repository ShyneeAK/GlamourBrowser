namespace GlamourBrowser.Core.Ui;

/// <summary>The grid size picker: which cell the mouse is over.</summary>
public static class GridPick
{
    /// <param name="x">Mouse position relative to the top-left of the first cell.</param>
    /// <param name="step">Cell size plus the gap to the next cell.</param>
    /// <returns>Columns × rows up to that cell (1-based), null outside the grid.</returns>
    public static (int Columns, int Rows)? CellAt(float x, float y, float step, int maxColumns, int maxRows)
    {
        if (x < 0 || y < 0)
            return null;
        var (columns, rows) = ((int)(x / step) + 1, (int)(y / step) + 1);
        return columns > maxColumns || rows > maxRows ? null : (columns, rows);
    }
}

/// <summary>The range slider: a track with a lower and an upper handle between <c>lo</c> and <c>hi</c>.</summary>
public static class RangeTrack
{
    /// <summary>The whole value at a position on the track, clamped to <paramref name="lo"/>–<paramref name="hi"/>.</summary>
    public static int ValueAt(float x, float trackMin, float trackWidth, int lo, int hi)
    {
        if (trackWidth <= 0)
            return lo;
        var value = MathF.Round(lo + (x - trackMin) / trackWidth * (hi - lo), MidpointRounding.AwayFromZero);
        return (int)Math.Clamp(value, lo, hi);
    }

    public static float PositionOf(int value, float trackMin, float trackWidth, int lo, int hi)
        => hi == lo ? trackMin : trackMin + (float)(value - lo) / (hi - lo) * trackWidth;

    /// <summary>
    /// Whether a press at <paramref name="x"/> takes the upper handle: the nearer one. On one spot the one that can move
    /// (both at the end: the lower one; both at the start: the upper one), otherwise the side of the press decides.
    /// </summary>
    public static bool GrabsUpper(float x, float minX, float maxX, bool upperAtEnd, bool lowerAtStart)
    {
        if (minX != maxX)
            return MathF.Abs(x - maxX) < MathF.Abs(x - minX);
        if (upperAtEnd)
            return false;
        return lowerAtStart || x > maxX;
    }

    /// <summary>
    /// How far the grabbed handle's centre is from the press: a press on the knob keeps that distance while dragging,
    /// so a click without moving changes nothing; a press on the track beside it makes the handle jump there (0).
    /// </summary>
    public static float GrabOffset(float pressX, float handleX, float radius)
        => MathF.Abs(handleX - pressX) <= radius ? handleX - pressX : 0f;

    /// <summary>A typed bound, clamped to <paramref name="lo"/>–<paramref name="hi"/>; it pushes the other bound along instead of stopping at it.</summary>
    public static (int Min, int Max) Typed(int min, int max, bool upper, int typed, int lo, int hi)
    {
        var value = Math.Clamp(typed, lo, hi);
        return upper ? (Math.Min(min, value), value) : (value, Math.Max(max, value));
    }

    /// <summary>Moves one handle to <paramref name="value"/>; it stops at the other one instead of crossing it.</summary>
    public static (int Min, int Max) Drag(int min, int max, bool upper, int value)
        => upper ? (min, Math.Max(value, min)) : (Math.Min(value, max), max);
}
