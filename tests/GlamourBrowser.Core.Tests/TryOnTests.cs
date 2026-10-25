using GlamourBrowser.Core.CustomSets;
using GlamourBrowser.Core.Items;
using GlamourBrowser.Core.Sets;

namespace GlamourBrowser.Core.Tests;

public class TryOnTests
{
    private const byte Black = 81;
    private const byte White = 82;

    private static CustomPiece Piece(EquipSlot slot, uint item, byte stain0 = 0, byte stain1 = 0) => new(slot, item, stain0, stain1);

    private static GlamourItem Item(uint id, EquipSlot slot, int dyeCount = 2)
        => new(id, $"Item {id}", 0, slot, new HashSet<uint>(), 70, dyeCount, RaceGenderRestriction.None);

    private static SetSubject Set(SetKind kind, params GlamourItem[] pieces)
        => new(new GlamourSet(new SetId(kind, 1), "Set", pieces.Select(p => new SetPiece(p.Id, p.Slot)).ToList(), 70), pieces);

    private static readonly IReadOnlyList<CustomPiece> Basis =
        [Piece(EquipSlot.Head, 1, 3), Piece(EquipSlot.Body, 2), Piece(EquipSlot.RingRight, 10), Piece(EquipSlot.RingLeft, 11)];

    [Fact]
    public void Pieces_WithoutClicksIsTheBasis()
    {
        var tryOn = new TryOn(Basis);

        Assert.Equal(Basis, tryOn.Pieces);
        Assert.False(tryOn.HasChanges);
    }

    [Fact]
    public void Put_ReplacesOnlyItsSlot()
    {
        var tryOn = new TryOn(Basis);
        tryOn.Put(Item(20, EquipSlot.Body), default);

        Assert.Equal([Piece(EquipSlot.Head, 1, 3), Piece(EquipSlot.Body, 20), Piece(EquipSlot.RingRight, 10), Piece(EquipSlot.RingLeft, 11)], tryOn.Pieces);
        Assert.True(tryOn.HasChanges);
    }

    [Theory]
    [InlineData(0, 0, 0)]
    [InlineData(1, 5, 0)]
    [InlineData(2, 5, 6)]
    public void Put_DyesTheChannelsTheItemHas(int dyeCount, byte stain0, byte stain1)
    {
        var tryOn = new TryOn(Basis);
        tryOn.Put(Item(20, EquipSlot.Body, dyeCount), new DyePair(5, 6));

        Assert.Equal(Piece(EquipSlot.Body, 20, stain0, stain1), tryOn.PieceIn(EquipSlot.Body));
    }

    [Fact]
    public void Put_WithoutDyeComesUndyedEvenOverADyedPiece()
    {
        var tryOn = new TryOn(Basis);
        tryOn.Put(Item(20, EquipSlot.Body), new DyePair(Black, White));
        tryOn.Put(Item(21, EquipSlot.Body), default);

        Assert.Equal(Piece(EquipSlot.Body, 21), tryOn.PieceIn(EquipSlot.Body));
    }

    [Fact]
    public void Put_RingsGoRightThenLeftThenRight()
    {
        var tryOn = new TryOn(Basis);
        tryOn.Put(Item(30, EquipSlot.RingRight), default);
        tryOn.Put(Item(31, EquipSlot.RingRight), default);
        tryOn.Put(Item(32, EquipSlot.RingRight), default);

        Assert.Equal(32u, tryOn.PieceIn(EquipSlot.RingRight)?.ItemId);
        Assert.Equal(31u, tryOn.PieceIn(EquipSlot.RingLeft)?.ItemId);
    }

    [Fact]
    public void PutAll_CustomSetKeepsItsDyes_DetectedSetTakesTheTileDye()
    {
        var hands = Item(40, EquipSlot.Hands, 1);
        var custom = Set(SetKind.Custom, hands) with { Dyes = new Dictionary<EquipSlot, (byte, byte)> { [EquipSlot.Hands] = (7, 0) } };
        var tryOn = new TryOn(Basis);

        tryOn.PutAll(custom, new DyePair(Black, White));
        Assert.Equal(Piece(EquipSlot.Hands, 40, 7), tryOn.PieceIn(EquipSlot.Hands));

        tryOn.PutAll(Set(SetKind.Dresser, hands), new DyePair(Black, White));
        Assert.Equal(Piece(EquipSlot.Hands, 40, Black), tryOn.PieceIn(EquipSlot.Hands));
    }

    [Fact]
    public void SetStain_OnlyForChannelsTheItemHas()
    {
        var tryOn = new TryOn(Basis);
        tryOn.Put(Item(20, EquipSlot.Body, 1), default);

        Assert.True(tryOn.SetStain(EquipSlot.Body, 1, 9, 1));
        Assert.False(tryOn.SetStain(EquipSlot.Body, 2, 9, 1));
        Assert.Equal(Piece(EquipSlot.Body, 20, 9), tryOn.PieceIn(EquipSlot.Body));
    }

    [Fact]
    public void SetStain_OnABasisPieceTakesItOver()
    {
        var tryOn = new TryOn(Basis);

        Assert.True(tryOn.SetStain(EquipSlot.Head, 2, 9, 2));
        tryOn.SetBasis([Piece(EquipSlot.Head, 50), Piece(EquipSlot.Body, 2)]);

        Assert.Equal(Piece(EquipSlot.Head, 1, 3, 9), tryOn.PieceIn(EquipSlot.Head));
    }

    [Fact]
    public void SetBasis_KeepsClickedSlots()
    {
        var tryOn = new TryOn(Basis);
        tryOn.Put(Item(20, EquipSlot.Body), default);
        tryOn.SetBasis([Piece(EquipSlot.Head, 60), Piece(EquipSlot.Body, 61)]);

        Assert.Equal([Piece(EquipSlot.Head, 60), Piece(EquipSlot.Body, 20)], tryOn.Pieces);
    }

    [Fact]
    public void Reset_DropsAllClicks()
    {
        var tryOn = new TryOn(Basis);
        tryOn.Put(Item(20, EquipSlot.Body), default);
        tryOn.Reset([Piece(EquipSlot.Feet, 70)]);

        Assert.Equal([Piece(EquipSlot.Feet, 70)], tryOn.Pieces);
        Assert.False(tryOn.HasChanges);
    }

    [Fact]
    public void SlotOf_FindsTheClickedRing()
    {
        var tryOn = new TryOn(Basis);
        tryOn.Put(Item(30, EquipSlot.RingRight), default);
        tryOn.Put(Item(31, EquipSlot.RingRight), default);

        Assert.Equal(EquipSlot.RingLeft, tryOn.SlotOf(31));
        Assert.Equal(EquipSlot.Head, tryOn.SlotOf(1));
        Assert.Null(tryOn.SlotOf(999));
    }

    [Fact]
    public void Changed_FiresOnEveryChange()
    {
        var tryOn = new TryOn(Basis);
        var count = 0;
        tryOn.Changed += () => count++;

        tryOn.Put(Item(20, EquipSlot.Body), default);
        tryOn.PutAll(Set(SetKind.Dresser, Item(40, EquipSlot.Hands)), default);
        tryOn.SetStain(EquipSlot.Body, 1, 9, 2);
        tryOn.SetBasis([Piece(EquipSlot.Feet, 70)]);
        tryOn.Reset(Basis);
        Assert.Equal(5, count);

        tryOn.SetBasis(Basis.ToList());
        Assert.Equal(5, count);
    }

    [Fact]
    public void KeyHash_SameWhenOnlyExceptedSlotsChange()
    {
        var tryOn = new TryOn(Basis);
        var body = SlotMask.Of([EquipSlot.Body]);
        var before = tryOn.KeyHash(body);
        var all = tryOn.KeyHash(0);

        tryOn.Put(Item(20, EquipSlot.Body), default);

        Assert.Equal(before, tryOn.KeyHash(body));
        Assert.NotEqual(all, tryOn.KeyHash(0));
    }
}
