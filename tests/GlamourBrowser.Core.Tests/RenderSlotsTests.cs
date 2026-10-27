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

    [Fact]
    public void SharedSlots_OnlyWhileTheGameWindowsAreClosed()
    {
        Assert.Equal([7u, 6u, 5u], RenderSlots.Select(5, liveReserved: false));
        Assert.Equal([7u, 6u, 5u, 4u, 3u], RenderSlots.Select(5, liveReserved: false, sharedFree: true));
    }

    [Fact]
    public void SharedSlots_TheRarelyUsedWindowsFirst_CharacterLast()
        => Assert.Equal([6u, 5u, 4u, 3u, 1u, 2u, 0u], RenderSlots.Select(7, liveReserved: true, sharedFree: true));

    [Fact]
    public void SharedSlots_AreZeroToFour()
    {
        Assert.True(RenderSlots.IsShared(0));
        Assert.True(RenderSlots.IsShared(4));
        Assert.False(RenderSlots.IsShared(5));
    }
}
