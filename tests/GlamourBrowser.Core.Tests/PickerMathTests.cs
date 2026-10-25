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
        => Assert.Equal(upper, RangeTrack.GrabsUpper(x, minX, maxX, upperAtEnd: false, lowerAtStart: false));

    [Theory]
    // Both handles at the end (100–100): only the lower one can move, from either side of the knob.
    [InlineData(115f, true, false, false)]
    [InlineData(105f, true, false, false)]
    // Both at the start (1–1): only the upper one can move.
    [InlineData(5f, false, true, true)]
    [InlineData(15f, false, true, true)]
    public void RangeTrack_HandlesOnOneSpotAtAnEnd_TheMovableOneMoves(float x, bool upperAtEnd, bool lowerAtStart, bool upper)
        => Assert.Equal(upper, RangeTrack.GrabsUpper(x, 10f, 10f, upperAtEnd, lowerAtStart));

    [Theory]
    // A press on the knob keeps its distance to the knob's centre, so a click without moving changes nothing.
    [InlineData(52f, 50f, 6f, -2f)]
    [InlineData(47f, 50f, 6f, 3f)]
    // A press on the track beside it lets the handle jump there.
    [InlineData(70f, 50f, 6f, 0f)]
    public void RangeTrack_GrabOffset(float pressX, float handleX, float radius, float expected)
        => Assert.Equal(expected, RangeTrack.GrabOffset(pressX, handleX, radius));

    [Theory]
    // A typed value pushes the other bound instead of stopping at it.
    [InlineData(false, 60, 60, 60)]
    [InlineData(false, 10, 10, 50)]
    [InlineData(true, 5, 5, 5)]
    [InlineData(true, 80, 20, 80)]
    [InlineData(true, 500, 20, 100)]
    [InlineData(false, -3, 1, 50)]
    public void RangeTrack_Typed(bool upper, int typed, int expectedMin, int expectedMax)
        => Assert.Equal((expectedMin, expectedMax), RangeTrack.Typed(20, 50, upper, typed, 1, 100));

    [Theory]
    [InlineData(false, 70, 70, 90)]
    [InlineData(false, 95, 90, 90)]
    [InlineData(true, 40, 50, 50)]
    [InlineData(true, 60, 50, 60)]
    public void RangeTrack_HandlesDoNotCross(bool upper, int value, int expectedMin, int expectedMax)
        => Assert.Equal((expectedMin, expectedMax), RangeTrack.Drag(50, 90, upper, value));
}
