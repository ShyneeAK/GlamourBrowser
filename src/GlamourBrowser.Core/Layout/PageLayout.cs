namespace GlamourBrowser.Core.Layout;

public readonly record struct PageLayoutResult(float TileWidth, float TileHeight, int RenderWidth);

/// <summary>Fits columns × rows tiles (CharaView aspect 576×960 plus a name label) into the available area.</summary>
public static class PageLayout
{
    public const float HeightPerWidth = 960f / 576f;

    public static readonly int[] RenderSteps = [128, 192, 256, 384, 576];

    public const int MinColumns = 2;
    public const int MaxColumns = 10;
    public const int MinRows = 1;
    public const int MaxRows = 6;

    /// <summary>Columns 2–10, rows 1–6, whatever a slider or an edited config delivers.</summary>
    public static (int Columns, int Rows) ClampGrid(int columns, int rows)
        => (Math.Clamp(columns, MinColumns, MaxColumns), Math.Clamp(rows, MinRows, MaxRows));

    public static PageLayoutResult Compute(int columns, int rows, float availableWidth, float availableHeight, float spacing, float labelHeight)
    {
        var byWidth = (availableWidth - (columns - 1) * spacing) / columns;
        var byHeight = ((availableHeight - (rows - 1) * spacing) / rows - labelHeight) / HeightPerWidth;
        var width = MathF.Max(0f, MathF.Min(byWidth, byHeight));
        return new PageLayoutResult(width, width * HeightPerWidth, RenderStepFor(width));
    }

    private static int RenderStepFor(float width)
    {
        foreach (var step in RenderSteps)
        {
            if (step >= width)
                return step;
        }
        return RenderSteps[^1];
    }
}
