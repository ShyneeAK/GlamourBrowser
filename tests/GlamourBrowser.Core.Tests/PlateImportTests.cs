using GlamourBrowser.Core.CustomSets;
using static GlamourBrowser.Core.Items.EquipSlot;

namespace GlamourBrowser.Core.Tests;

public class PlateImportTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 25, 16, 0, 0, TimeSpan.FromHours(1));
    private static readonly Dictionary<uint, uint> Variants = new() { [12] = 10 };

    [Fact]
    public void NewSets_SkipsEmptyPlates_AndKeepsThePlateNumber()
    {
        IReadOnlyList<IReadOnlyList<CustomPiece>> plates = [[], [new CustomPiece(Body, 10, 1, 0)], [new CustomPiece(Head, 20, 0, 0)]];
        Assert.Equal([1, 2], PlateImport.NewSets(plates, [], Variants).Select(p => p.Plate));
    }

    [Fact]
    public void NewSets_SkipsAPlateAnExistingSetAlreadyHas()
    {
        // The plate holds the "+1" variant 12 with the same dye; the stored set has its base 10.
        var existing = new CustomSet(1, "Glamour-Plate 1", [new CustomPiece(Body, 10, 1, 0), new CustomPiece(Head, 20, 0, 0)], Now);
        IReadOnlyList<IReadOnlyList<CustomPiece>> plates = [[new CustomPiece(Head, 20, 0, 0), new CustomPiece(Body, 12, 1, 0)], [new CustomPiece(Body, 10, 2, 0)]];
        var imported = PlateImport.NewSets(plates, [existing], Variants);
        Assert.Equal([1], imported.Select(p => p.Plate));
    }

    [Fact]
    public void NewSets_SameContentTwice_ImportsItOnce()
    {
        IReadOnlyList<IReadOnlyList<CustomPiece>> plates = [[new CustomPiece(Body, 10, 1, 0)], [new CustomPiece(Body, 12, 1, 0)]];
        Assert.Equal([0], PlateImport.NewSets(plates, [], Variants).Select(p => p.Plate));
    }

    [Fact]
    public void NewSets_GivesThePiecesWithTheirBase()
        => Assert.Equal([new CustomPiece(Body, 10, 1, 0)], PlateImport.NewSets([[new CustomPiece(Body, 12, 1, 0)]], [], Variants).Single().Pieces);
}
