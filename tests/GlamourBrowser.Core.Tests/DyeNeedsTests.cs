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
        var needs = DyeNeeds.Of([(1, 2, 2), (101, 0, 2), (86, 36, 2), (101, 0, 1)], ItemsOf);

        Assert.Equal([new DyeNeed(Generic, 3), new DyeNeed(CloudWhite, 2), new DyeNeed(Extra1, 1)], needs);
    }

    [Fact]
    public void LeavesOutUndyedChannels_ChannelsTheItemDoesNotHave_AndUnknownDyes()
    {
        var needs = DyeNeeds.Of([(0, 0, 2), (5, 7, 1), (5, 7, 0), (120, 0, 1)], ItemsOf);

        Assert.Equal([new DyeNeed(Generic, 1)], needs);
    }
}
