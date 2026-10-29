using GlamourBrowser.Core.CustomSets;

namespace GlamourBrowser.Core.Tests;

public class DyeNeedsTests
{
    private const uint Generic = 52254;     // Einfacher Farbstoff
    private const uint Extra1 = 52255;      // Zusatzfarbstoff 1
    private const uint CloudWhite = 13114;  // Wolkenweißer Farbstoff

    private static IReadOnlyList<uint> ItemsOf(byte stain) => stain switch
    {
        <= 85 => [Generic],
        <= 94 => [Extra1],
        101 => [CloudWhite, 8732],
        _ => [],
    };

    [Fact]
    public void CountsEachDyedChannelByItsDyeItem_TheMostNeededFirst()
    {
        var tally = DyeNeeds.Of([new(1, 2, 2), new(101, 0, 2), new(86, 36, 2), new(101, 0, 1)], ItemsOf);

        Assert.Equal([new DyeNeed(Generic, 3), new DyeNeed(CloudWhite, 2), new DyeNeed(Extra1, 1)], tally.Needs);
        Assert.Equal((6, 0), (tally.Total, tally.Done));
    }

    [Fact]
    public void LeavesOutUndyedChannels_ChannelsTheItemDoesNotHave_AndUnknownDyes()
    {
        var tally = DyeNeeds.Of([new(0, 0, 2), new(5, 7, 1), new(5, 7, 0), new(120, 0, 1)], ItemsOf);

        Assert.Equal([new DyeNeed(Generic, 1)], tally.Needs);
        Assert.Equal(1, tally.Total);
    }

    [Fact]
    public void AChannelDyedSoInTheDresser_CountsAsDone()
    {
        // Body: both channels dyed already; legs: the first one, the second differs; feet: dyed otherwise.
        var tally = DyeNeeds.Of([new(1, 101, 2, Have0: 1, Have1: 101), new(2, 3, 2, Have0: 2, Have1: 9), new(4, 0, 1, Have0: 7)], ItemsOf);

        Assert.Equal((5, 3), (tally.Total, tally.Done));
        Assert.Equal([new DyeNeed(Generic, 2)], tally.Needs);
    }
}
