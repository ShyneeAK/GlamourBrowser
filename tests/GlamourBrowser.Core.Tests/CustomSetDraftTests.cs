using GlamourBrowser.Core.CustomSets;
using GlamourBrowser.Core.Items;
using GlamourBrowser.Core.Sets;
using static GlamourBrowser.Core.Items.EquipSlot;

namespace GlamourBrowser.Core.Tests;

public class CustomSetDraftTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 25, 15, 0, 0, TimeSpan.FromHours(1));

    private static GlamourItem Item(uint id, EquipSlot slot, int dyes = 2) => new(id, $"Item {id}", 0, slot, new HashSet<uint>(), 50, dyes, RaceGenderRestriction.None);

    private static readonly GlamourItem Robe = Item(10, Body);
    private static readonly GlamourItem Ring1 = Item(30, RingRight, 0);
    private static readonly GlamourItem Ring2 = Item(31, RingRight, 0);
    private static readonly GlamourItem Ring3 = Item(32, RingRight, 0);

    private static CustomSetDraft Empty() => new(null, "Neues Set", []);

    [Fact]
    public void Put_ItemGoesToItsSlot()
    {
        var draft = Empty();
        draft.Put(Robe, selectedSlot: Head);
        Assert.Equal(new CustomPiece(Body, 10, 0, 0), Assert.Single(draft.Pieces));
    }

    [Fact]
    public void Put_RingGoesToTheSelectedRingSlot()
    {
        var draft = Empty();
        draft.Put(Ring1, selectedSlot: RingLeft);
        Assert.Equal(RingLeft, Assert.Single(draft.Pieces).Slot);
    }

    [Fact]
    public void Put_RingFillsTheFirstFreeRingSlot()
    {
        var draft = Empty();
        draft.Put(Ring1, null);
        draft.Put(Ring2, null);
        Assert.Equal([(RingRight, 30u), (RingLeft, 31u)], draft.Pieces.Select(p => (p.Slot, p.ItemId)));
    }

    [Fact]
    public void Put_RingReplacesTheRightRing_WhenBothAreSet()
    {
        var draft = Empty();
        draft.Put(Ring1, null);
        draft.Put(Ring2, null);
        draft.Put(Ring3, null);
        Assert.Equal([(RingRight, 32u), (RingLeft, 31u)], draft.Pieces.Select(p => (p.Slot, p.ItemId)));
    }

    [Theory]
    [InlineData(2, 5, 6)]
    [InlineData(1, 5, 0)]
    [InlineData(0, 0, 0)]
    public void Put_KeepsDyesTheNewItemCanTake(int dyeCount, byte stain0, byte stain1)
    {
        var draft = new CustomSetDraft(null, "Set", [new CustomPiece(Body, 10, 5, 6)]);
        draft.Put(Item(11, Body, dyeCount), null);
        Assert.Equal(new CustomPiece(Body, 11, stain0, stain1), Assert.Single(draft.Pieces));
    }

    [Fact]
    public void SetStain_OnlyForExistingChannels()
    {
        var draft = Empty();
        draft.Put(Item(11, Body, 1), null);
        Assert.True(draft.SetStain(Body, 1, 7, dyeCount: 1));
        Assert.False(draft.SetStain(Body, 2, 8, dyeCount: 1));
        Assert.False(draft.SetStain(Head, 1, 7, dyeCount: 2));
        Assert.Equal(new CustomPiece(Body, 11, 7, 0), draft.PieceIn(Body));
    }

    [Fact]
    public void PutAll_TakesPiecesAndDyes_AndKeepsOtherSlots()
    {
        var other = CustomSetSubjects.ToSubject(new CustomSet(2, "Anderes", [new CustomPiece(Body, 10, 3, 4)], Now),
            id => id == 10 ? Robe : null)!;
        var draft = Empty();
        draft.Put(Item(20, Head), null);
        draft.PutAll(other);
        Assert.Equal([new CustomPiece(Head, 20, 0, 0), new CustomPiece(Body, 10, 3, 4)], draft.Pieces);
    }

    [Fact]
    public void Remove_ClearsTheSlot()
    {
        var draft = Empty();
        draft.Put(Robe, null);
        draft.Remove(Body);
        Assert.Empty(draft.Pieces);
        Assert.Null(draft.PieceIn(Body));
    }

    [Fact]
    public void IsDirty_AfterChangesOnly()
    {
        var draft = CustomSetDraft.From(new CustomSet(4, "Alt", [new CustomPiece(Body, 10, 0, 0)], Now));
        Assert.False(draft.IsDirty);
        draft.Put(Item(20, Head), null);
        Assert.True(draft.IsDirty);
        draft.Remove(Head);
        Assert.False(draft.IsDirty);
        draft.Name = "Neu";
        Assert.True(draft.IsDirty);
    }

    [Fact]
    public void UnknownPieces_SurviveEditing()
    {
        var draft = CustomSetDraft.From(new CustomSet(4, "Alt", [new CustomPiece(Head, 4_000_000, 1, 0)], Now));
        draft.Put(Robe, null);
        Assert.Contains(new CustomPiece(Head, 4_000_000, 1, 0), draft.Pieces);
    }

    [Fact]
    public void ToSubject_UsesTheDraftsIdAndDyes()
    {
        var draft = new CustomSetDraft(4, "Entwurf", [new CustomPiece(Body, 10, 2, 0)]);
        var subject = draft.ToSubject(id => id == 10 ? Robe : null)!;
        Assert.Equal(new SetId(SetKind.Custom, 4), subject.Set.Id);
        Assert.Equal(((byte)2, (byte)0), subject.StainsOf(Body));
        Assert.Null(Empty().ToSubject(_ => Robe));
    }
}
