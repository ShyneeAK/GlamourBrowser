using GlamourBrowser.Core.CustomSets;
using static GlamourBrowser.Core.Items.EquipSlot;

namespace GlamourBrowser.Core.Tests;

public class PlateTransferTests
{
    private static string DyeName(byte id) => id switch { 0 => "keine Farbe", 1 => "Schneeweiß", 2 => "Rußschwarz", _ => $"Farbe {id}" };

    private static readonly Dictionary<uint, IReadOnlyList<string>> Places = new() { [40] = ["Gehilfe Mira"] };

    private static IReadOnlyList<PlateSlot> Plan(IReadOnlyList<CustomPiece> pieces, Func<uint, PlateSource?>? dresser = null, Func<uint, PlateSource?>? armoire = null)
        => PlateTransfer.Plan(pieces, dresser ?? (_ => null), armoire ?? (_ => null), id => Places.GetValueOrDefault(id) ?? [], DyeName);

    private static PlateSlot SlotOf(IReadOnlyList<PlateSlot> plan, Items.EquipSlot slot) => plan.Single(s => s.Slot == slot);

    [Fact]
    public void Plan_DresserBeforeArmoire()
    {
        var plan = Plan([new CustomPiece(Body, 10, 0, 0)],
            dresser: id => id == 10 ? new PlateSource(PlateSourceKind.Dresser, 6, 10, 0, 0) : null,
            armoire: id => id == 10 ? new PlateSource(PlateSourceKind.Armoire, 1, 10, 0, 0) : null);
        Assert.Equal(PlateSourceKind.Dresser, SlotOf(plan, Body).Source!.Value.Kind);
    }

    [Fact]
    public void Plan_MissingWithPlace_OrNotOwned()
    {
        var plan = Plan([new CustomPiece(Body, 40, 0, 0), new CustomPiece(Head, 41, 0, 0)]);
        Assert.Equal("liegt in: Gehilfe Mira", SlotOf(plan, Body).Missing);
        Assert.Equal("nicht im Besitz", SlotOf(plan, Head).Missing);
        Assert.Null(SlotOf(plan, Body).Source);
    }

    [Fact]
    public void Plan_EmptySetSlotsClearThePlate()
    {
        var plan = Plan([new CustomPiece(Body, 10, 0, 0)], dresser: _ => new PlateSource(PlateSourceKind.Dresser, 6, 10, 0, 0));
        Assert.Equal(PlateSlots.Order, plan.Select(s => s.Slot));
        var head = SlotOf(plan, Head);
        Assert.Null(head.Piece);
        Assert.Null(head.Source);
        Assert.Null(head.Missing);
    }

    [Fact]
    public void Plan_DyeNote_WhenTheStoredDyeDiffers()
    {
        var plan = Plan([new CustomPiece(Body, 10, 1, 0), new CustomPiece(Head, 20, 1, 2), new CustomPiece(Hands, 30, 0, 0)],
            dresser: id => new PlateSource(PlateSourceKind.Dresser, id, id, id == 30 ? (byte)1 : (byte)0, 0));
        Assert.Equal("im Spiel färben: Schneeweiß", SlotOf(plan, Body).DyeNote);
        Assert.Equal("im Spiel färben: Schneeweiß / Rußschwarz", SlotOf(plan, Head).DyeNote);
        Assert.Equal("im Spiel färben: keine Farbe", SlotOf(plan, Hands).DyeNote);
        Assert.Null(Plan([new CustomPiece(Body, 10, 1, 0)], dresser: _ => new PlateSource(PlateSourceKind.Dresser, 6, 10, 1, 0))[3].DyeNote);
    }

    [Fact]
    public void Dresser_FindsHqVariantAndOutfitPieces()
    {
        // Raw dresser ids: 0 empty, an HQ "+1" variant 12 of base 10, outfit 500 holding 21 and 22, a plain 30.
        var dresserOf = PlateSources.Dresser([0, 1_000_012, 500, 30], [0, 3, 7, 0], [0, 0, 8, 0],
            new Dictionary<uint, IReadOnlyList<uint>> { [500] = [21, 22] }, new Dictionary<uint, uint> { [12] = 10 });

        Assert.Equal(new PlateSource(PlateSourceKind.Dresser, 1, 1_000_012, 3, 0), dresserOf(10));
        Assert.Equal(new PlateSource(PlateSourceKind.Dresser, 2, 22, 7, 8), dresserOf(22));
        Assert.Equal(new PlateSource(PlateSourceKind.Dresser, 3, 30, 0, 0), dresserOf(30));
        Assert.Null(dresserOf(99));
    }

    [Fact]
    public void Dresser_PrefersTheItemItselfOverAVariant_AndOverAnOutfit()
    {
        var dresserOf = PlateSources.Dresser([12, 500, 10], [0, 0, 0], [0, 0, 0],
            new Dictionary<uint, IReadOnlyList<uint>> { [500] = [10] }, new Dictionary<uint, uint> { [12] = 10 });
        Assert.Equal(2u, dresserOf(10)!.Value.Id);
    }

    [Fact]
    public void Armoire_FindsTheItemOrAVariant()
    {
        var armoireOf = PlateSources.Armoire([(1u, 3220u), (2u, 12u)], new Dictionary<uint, uint> { [12] = 10 });
        Assert.Equal(new PlateSource(PlateSourceKind.Armoire, 1, 3220, 0, 0), armoireOf(3220));
        Assert.Equal(new PlateSource(PlateSourceKind.Armoire, 2, 12, 0, 0), armoireOf(10));
    }

    [Fact]
    public void Read_NormalizesHq_SkipsEmpty_InPlateOrder()
    {
        uint[] ids = [28329, 0, 16121, 1_018_692, 0, 0, 0, 0, 0, 0, 39508, 16234];
        byte[] stain0 = [0, 0, 0, 5, 0, 0, 0, 0, 0, 0, 0, 0];
        byte[] stain1 = new byte[12];
        Assert.Equal([new CustomPiece(MainHand, 28329, 0, 0), new CustomPiece(Head, 16121, 0, 0), new CustomPiece(Body, 18692, 5, 0),
                      new CustomPiece(RingRight, 39508, 0, 0), new CustomPiece(RingLeft, 16234, 0, 0)],
            PlateTransfer.Read(ids, stain0, stain1));
    }

    private static string SlotName(Items.EquipSlot slot) => slot.ToString();

    private static string Name(uint id) => $"Item {id}";

    [Fact]
    public void Report_FailedClear_IsListed()
    {
        // The set has no head piece; the game ignored clearing the plate's head slot, so the old piece stays.
        var plan = Plan([new CustomPiece(Body, 10, 0, 0)], dresser: _ => new PlateSource(PlateSourceKind.Dresser, 6, 10, 0, 0));
        var report = PlateTransfer.Report(plan, done: [Body], failed: [Head], editorClosed: false, Name, SlotName);
        Assert.Equal((1, 1), (report.Transferred, report.Total));
        Assert.Equal([("Head", PlateTransfer.NotCleared)], report.Notes);
    }

    [Fact]
    public void Report_MissingPieceNotCleared_SaysBoth()
    {
        var plan = Plan([new CustomPiece(Body, 40, 0, 0)]);
        var report = PlateTransfer.Report(plan, done: [], failed: [Body], editorClosed: false, Name, SlotName);
        Assert.Equal([("Item 40", "liegt in: Gehilfe Mira"), ("Body", PlateTransfer.NotCleared)], report.Notes);
    }

    [Fact]
    public void Report_FailedSet_IsListed_AndDyeNotesOnlyForDone()
    {
        var plan = Plan([new CustomPiece(Body, 10, 1, 0), new CustomPiece(Head, 20, 1, 0)], dresser: id => new PlateSource(PlateSourceKind.Dresser, id, id, 0, 0));
        var report = PlateTransfer.Report(plan, done: [Body], failed: [Head], editorClosed: false, Name, SlotName);
        Assert.Equal([("Item 20", PlateTransfer.NotTaken), ("Item 10", "im Spiel färben: Schneeweiß")], report.Notes.OrderBy(n => n.Reason == PlateTransfer.NotTaken ? 0 : 1).ToList());
    }

    [Fact]
    public void Report_EditorClosed_ListsWhatWasNotDone()
    {
        var plan = Plan([new CustomPiece(Body, 10, 0, 0), new CustomPiece(Head, 20, 0, 0)], dresser: id => new PlateSource(PlateSourceKind.Dresser, id, id, 0, 0));
        var report = PlateTransfer.Report(plan, done: [Body], failed: [], editorClosed: true, Name, SlotName);
        Assert.Equal((1, 2), (report.Transferred, report.Total));
        Assert.Contains(("Item 20", PlateTransfer.NotDone), report.Notes);
        // Empty set slots that were not cleared yet are listed by slot.
        Assert.Contains(("MainHand", PlateTransfer.NotDone), report.Notes);
    }
}
