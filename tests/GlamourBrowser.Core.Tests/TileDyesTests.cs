using GlamourBrowser.Core.Items;
using GlamourBrowser.Core.Rendering;
using GlamourBrowser.Core.Sets;
using GlamourBrowser.Core.Ui;

namespace GlamourBrowser.Core.Tests;

public class TileDyesTests
{
    private const byte Black = 81;
    private const byte White = 82;
    private static readonly DyePair BlackWhite = new(Black, White);

    private static GlamourItem Item(uint id, EquipSlot slot, int dyeCount)
        => new(id, $"Item {id}", 0, slot, new HashSet<uint>(), 70, dyeCount, RaceGenderRestriction.None);

    private static SetSubject Set(SetKind kind, params GlamourItem[] pieces)
        => new(new GlamourSet(new SetId(kind, 1), "Set", pieces.Select(p => new SetPiece(p.Id, p.Slot)).ToList(), 70), pieces);

    [Theory]
    [InlineData(0, 0, 0)]
    [InlineData(1, Black, 0)]
    [InlineData(2, Black, White)]
    public void For_DyesOnlyChannelsTheItemHas(int dyeCount, byte stain0, byte stain1)
        => Assert.Equal((stain0, stain1), TileDyes.For(Item(1, EquipSlot.Body, dyeCount), BlackWhite));

    [Fact]
    public void For_NoDyeIsUndyed()
        => Assert.Equal(((byte)0, (byte)0), TileDyes.For(Item(1, EquipSlot.Body, 2), default));

    [Fact]
    public void For_OnlyTheSecondDyeSet_LeavesChannelOneUndyed()
        => Assert.Equal(((byte)0, White), TileDyes.For(Item(1, EquipSlot.Body, 2), new DyePair(0, White)));

    [Fact]
    public void Of_CustomSetKeepsItsOwnDyes()
    {
        var body = Item(1, EquipSlot.Body, 2);
        var set = Set(SetKind.Custom, body) with { Dyes = new Dictionary<EquipSlot, (byte, byte)> { [EquipSlot.Body] = (5, 6) } };

        Assert.Equal(((byte)5, (byte)6), TileDyes.Of(set, body, BlackWhite));
    }

    [Fact]
    public void Of_DetectedSetAndItemTakeTheTileDye()
    {
        var body = Item(1, EquipSlot.Body, 1);

        Assert.Equal((Black, (byte)0), TileDyes.Of(Set(SetKind.Dresser, body), body, BlackWhite));
        Assert.Equal((Black, (byte)0), TileDyes.Of(new ItemSubject(body), body, BlackWhite));
    }

    [Theory]
    [InlineData(0, false, false)]
    [InlineData(1, true, false)]
    [InlineData(2, true, true)]
    public void DyeBoxes_For(int dyeCount, bool first, bool second)
        => Assert.Equal((first, second), DyeBoxes.For(dyeCount));
}
