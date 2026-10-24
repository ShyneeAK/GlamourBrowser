using GlamourBrowser.Core.Rendering;

namespace GlamourBrowser.Core.Tests;

public class RenderSlotsTests
{
    [Fact]
    public void NoLiveReservation_TakesFromSeven() => Assert.Equal([7u, 6u, 5u], RenderSlots.Select(3, liveReserved: false));

    [Fact]
    public void OneView_WithLiveReserved_StillRendersOnSix() => Assert.Equal([6u], RenderSlots.Select(1, liveReserved: true));

    [Fact]
    public void TwoViews_WithLiveReserved_UseSixAndFive() => Assert.Equal([6u, 5u], RenderSlots.Select(2, liveReserved: true));

    [Fact]
    public void ThreeViews_WithLiveReserved_OnlyTwoRemain() => Assert.Equal([6u, 5u], RenderSlots.Select(3, liveReserved: true));

    [Fact]
    public void MaxViews_IsClamped() => Assert.Equal([7u], RenderSlots.Select(0, liveReserved: false));
}
