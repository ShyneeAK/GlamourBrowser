using GlamourBrowser.Core.CustomSets;
using GlamourBrowser.Core.Items;

namespace GlamourBrowser.Core.Tests;

public class LooksTests
{
    private static CustomPiece Piece(EquipSlot slot, uint item, byte stain0 = 0, byte stain1 = 0) => new(slot, item, stain0, stain1);

    [Fact]
    public void Overlay_OverWinsPerSlotAndOtherSlotsStay()
    {
        var look = Looks.Overlay([Piece(EquipSlot.Head, 1), Piece(EquipSlot.Body, 2)], [Piece(EquipSlot.Head, 3)]);

        Assert.Equal([Piece(EquipSlot.Head, 3), Piece(EquipSlot.Body, 2)], look);
    }

    [Fact]
    public void Overlay_DropsEmptyItemsAndSortsBySlot()
    {
        var look = Looks.Overlay([Piece(EquipSlot.Feet, 4), Piece(EquipSlot.Head, 0)], [Piece(EquipSlot.MainHand, 9)]);

        Assert.Equal([Piece(EquipSlot.MainHand, 9), Piece(EquipSlot.Feet, 4)], look);
    }

    private static readonly ushort OverLegsAndFeet = SlotMask.Of([EquipSlot.Legs, EquipSlot.Feet]);

    [Fact]
    public void Overlay_APieceFromAboveHidesTheSlotsItBlocks()
    {
        var look = Looks.Overlay([Piece(EquipSlot.Body, 2), Piece(EquipSlot.Legs, 3), Piece(EquipSlot.Feet, 4)], [Piece(EquipSlot.Body, 9)],
                                 item => item == 9 ? OverLegsAndFeet : (ushort)0);

        Assert.Equal([Piece(EquipSlot.Body, 9)], look);
    }

    [Fact]
    public void Overlay_ABasisPieceThatBlocksASlotFromAboveComesOff()
    {
        var look = Looks.Overlay([Piece(EquipSlot.Head, 1), Piece(EquipSlot.Body, 2)], [Piece(EquipSlot.Legs, 3)],
                                 item => item == 2 ? OverLegsAndFeet : (ushort)0);

        Assert.Equal([Piece(EquipSlot.Head, 1), Piece(EquipSlot.Legs, 3)], look);
    }

    [Fact]
    public void Overlay_ABasisPieceHidesTheBasisPiecesInTheSlotsItBlocks()
    {
        // A robe glamoured onto the worn top: the game keeps the legs and feet worn under it.
        IReadOnlyList<CustomPiece> worn = [Piece(EquipSlot.Head, 1), Piece(EquipSlot.Body, 9), Piece(EquipSlot.Legs, 3), Piece(EquipSlot.Feet, 4)];
        ushort BlocksOf(uint item) => item == 9 ? OverLegsAndFeet : (ushort)0;

        Assert.Equal([Piece(EquipSlot.Head, 1), Piece(EquipSlot.Body, 9)], Looks.Overlay(worn, [], BlocksOf));
        // A legs tile takes the robe off, and the feet under it show again.
        Assert.Equal([Piece(EquipSlot.Head, 1), Piece(EquipSlot.Feet, 4)], Looks.Overlay(worn, [], BlocksOf, SlotMask.Of(EquipSlot.Legs)));
    }

    [Fact]
    public void Overlay_CoverLeavesItsSlotsAndTheirBlockersOut()
    {
        var look = Looks.Overlay([Piece(EquipSlot.Head, 1), Piece(EquipSlot.Body, 2), Piece(EquipSlot.Feet, 4)], [Piece(EquipSlot.Body, 9)],
                                 item => item == 9 ? OverLegsAndFeet : (ushort)0, SlotMask.Of(EquipSlot.Feet));

        Assert.Equal([Piece(EquipSlot.Head, 1), Piece(EquipSlot.Body, 2)], look);
    }

    [Fact]
    public void Hash_IgnoresTheExceptedSlots()
    {
        var head = SlotMask.Of([EquipSlot.Head]);

        Assert.Equal(Looks.Hash([Piece(EquipSlot.Head, 1), Piece(EquipSlot.Body, 2)], head),
                     Looks.Hash([Piece(EquipSlot.Head, 7, 5), Piece(EquipSlot.Body, 2)], head));
    }

    [Fact]
    public void Hash_ChangesWithAnotherSlotOrADye()
    {
        var head = SlotMask.Of([EquipSlot.Head]);
        var basis = Looks.Hash([Piece(EquipSlot.Head, 1), Piece(EquipSlot.Body, 2)], head);

        Assert.NotEqual(basis, Looks.Hash([Piece(EquipSlot.Head, 1), Piece(EquipSlot.Body, 3)], head));
        Assert.NotEqual(basis, Looks.Hash([Piece(EquipSlot.Head, 1), Piece(EquipSlot.Body, 2, 5)], head));
    }

    [Fact]
    public void Hash_IsNeverZero()
        => Assert.NotEqual(0u, Looks.Hash([]));
}
