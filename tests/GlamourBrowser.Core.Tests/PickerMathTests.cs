using GlamourBrowser.Core.Layout;
using GlamourBrowser.Core.Ui;

namespace GlamourBrowser.Core.Tests;

public class PickerMathTests
{
    // Cells of 20 px plus 4 px gap: a step of 24 px.
    [Theory]
    [InlineData(0f, 0f, 1, 1)]
    [InlineData(23.9f, 0f, 1, 1)]
    [InlineData(24f, 0f, 2, 1)]
    [InlineData(5.5f * 24, 2.1f * 24, 6, 3)]
    [InlineData(10 * 24 - 1, 6 * 24 - 1, 10, 6)]
    public void GridPick_CellUnderTheMouse(float x, float y, int columns, int rows)
        => Assert.Equal((columns, rows), GridPick.CellAt(x, y, 24, PageLayout.MaxColumns, PageLayout.MaxRows));

    [Theory]
    [InlineData(-1f, 5f)]
    [InlineData(5f, -1f)]
    [InlineData(10 * 24f, 5f)]
    [InlineData(5f, 6 * 24f)]
    public void GridPick_OutsideIsNothing(float x, float y)
        => Assert.Null(GridPick.CellAt(x, y, 24, PageLayout.MaxColumns, PageLayout.MaxRows));

    // A track from x = 10 to 110 for levels 1–100.
    [Theory]
    [InlineData(10f, 1)]
    [InlineData(110f, 100)]
    [InlineData(60f, 51)]
    [InlineData(-50f, 1)]
    [InlineData(500f, 100)]
    public void RangeTrack_ValueAt(float x, int expected) => Assert.Equal(expected, RangeTrack.ValueAt(x, 10, 100, 1, 100));

    [Fact]
    public void RangeTrack_PositionOf_IsTheInverse()
    {
        Assert.Equal(10f, RangeTrack.PositionOf(1, 10, 100, 1, 100));
        Assert.Equal(110f, RangeTrack.PositionOf(100, 10, 100, 1, 100));
        Assert.Equal(50, RangeTrack.ValueAt(RangeTrack.PositionOf(50, 10, 100, 1, 100), 10, 100, 1, 100));
    }

    [Theory]
    [InlineData(20f, 30f, 80f, false)]
    [InlineData(70f, 30f, 80f, true)]
    [InlineData(54f, 30f, 80f, false)]
    [InlineData(56f, 30f, 80f, true)]
    // Both handles on one spot: left of it takes the lower one, right of it the upper one.
    [InlineData(40f, 50f, 50f, false)]
    [InlineData(60f, 50f, 50f, true)]
    public void RangeTrack_TheNearerHandleMoves(float x, float minX, float maxX, bool upper)
        => Assert.Equal(upper, RangeTrack.GrabsUpper(x, minX, maxX));

    [Theory]
    [InlineData(false, 70, 70, 90)]
    [InlineData(false, 95, 90, 90)]
    [InlineData(true, 40, 50, 50)]
    [InlineData(true, 60, 50, 60)]
    public void RangeTrack_HandlesDoNotCross(bool upper, int value, int expectedMin, int expectedMax)
        => Assert.Equal((expectedMin, expectedMax), RangeTrack.Drag(50, 90, upper, value));
}
