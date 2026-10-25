using GlamourBrowser.Core.Items;
using GlamourBrowser.Core.Sets;
using static GlamourBrowser.Core.Items.EquipSlot;

namespace GlamourBrowser.Core.Tests;

public class SetDetectionTests
{
    private static SetCandidate Candidate(uint id, string name, EquipSlot slot, ushort set = 100, byte variant = 1, uint ilvl = 500,
                                          uint cat = 64, uint[]? jobs = null, int level = 70)
        => new(id, name, slot, set, variant, ilvl, cat, (jobs ?? [19, 21]).ToHashSet(), level);

    private static SetCandidate[] Asuran() =>
    [
        Candidate(1, "Asuran-Hachigane des Spähens", Head),
        Candidate(2, "Asuran-Togi des Spähens", Body),
        Candidate(3, "Asuran-Sune-Ate des Spähens", Feet),
    ];

    private static uint[] Ids(GlamourSet set) => set.Pieces.Select(p => p.ItemId).ToArray();

    private static IReadOnlyList<GlamourSet> Build(IEnumerable<SetCandidate> items, SheetSet[]? dresser = null, SheetSet[]? mog = null)
        => SetDetection.Build(items.ToList(), dresser ?? [], mog ?? []);

    [Fact]
    public void Model_GroupsArmourWithSameModelVariantItemLevelAndCategory()
    {
        var sets = Build([.. Asuran(), Candidate(4, "Asuran-Tekko des Spähens", Hands, variant: 2)]);
        var set = Assert.Single(sets);
        Assert.Equal(SetKind.Model, set.Id.Kind);
        Assert.Equal([1u, 2u, 3u], Ids(set));
    }

    [Fact]
    public void Model_NeedsThreeSlots() => Assert.Empty(Build(Asuran().Take(2)));

    [Fact]
    public void Model_OnePiecePerSlot_PrefersNoDagger_ThenLowestId()
    {
        var set = Assert.Single(Build([Candidate(1, "Kappe", Head), Candidate(30, "Robe †", Body), Candidate(25, "Robe", Body),
                                       Candidate(20, "Robe", Body), Candidate(3, "Schuhe", Feet)]));
        Assert.Equal([1u, 20u, 3u], Ids(set));
    }

    [Fact]
    public void Model_IdIsLowestPieceId_LevelIsHighest()
    {
        var set = Assert.Single(Build([Candidate(41, "A-Robe", Body, level: 70), Candidate(40, "A-Kappe", Head, level: 70),
                                       Candidate(42, "A-Schuhe", Feet, level: 71)]));
        Assert.Equal(new SetId(SetKind.Model, 40), set.Id);
        Assert.Equal(71, set.Level);
    }

    [Fact]
    public void Model_NameFromCommonStart() => Assert.Equal("Asuran-Serie des Spähens", Assert.Single(Build(Asuran())).Name);

    [Fact]
    public void Model_NameFallsBackToBody()
    {
        var set = Assert.Single(Build([Candidate(10, "Gordische Krone der Magie", Head), Candidate(11, "Gordischer Talar der Magie", Body),
                                       Candidate(12, "Gordische Hose der Magie", Legs)]));
        Assert.Equal("Gordischer Talar der Magie (Serie)", set.Name);
    }

    [Fact]
    public void Accessory_Joins_WhenItemLevelNameStartAndJobsMatch()
    {
        var set = Assert.Single(Build([.. Asuran(), Candidate(5, "Asuran-Ohrringe des Zielens", Ears, set: 900, jobs: [19, 21, 23])]));
        Assert.Contains(5u, Ids(set));
    }

    [Fact]
    public void Accessory_Ignored_WhenJobsDoNotCover()
        => Assert.DoesNotContain(5u, Ids(Assert.Single(Build([.. Asuran(), Candidate(5, "Asuran-Ohrringe des Zielens", Ears, set: 900, jobs: [19])]))));

    [Fact]
    public void Accessory_Ignored_WhenItemLevelDiffers()
        => Assert.DoesNotContain(5u, Ids(Assert.Single(Build([.. Asuran(), Candidate(5, "Asuran-Ohrringe des Zielens", Ears, set: 900, ilvl: 510)]))));

    [Fact]
    public void Accessory_Ignored_WhenNoCommonStart()
    {
        var set = Assert.Single(Build([Candidate(10, "Gordische Krone der Magie", Head), Candidate(11, "Gordischer Talar der Magie", Body),
                                       Candidate(12, "Gordische Hose der Magie", Legs), Candidate(13, "Gordische Ohrringe der Magie", Ears, set: 900)]));
        Assert.DoesNotContain(13u, Ids(set));
    }

    [Fact]
    public void Dresser_PiecesAndNameFromSheet()
    {
        var items = new[] { Candidate(100, "Ao Oni-Somen", Head, set: 1), Candidate(101, "Ao Oni-Hara-Ate", Body, set: 2), Candidate(102, "Ao Oni-Kote", Hands, set: 3) };
        var set = Assert.Single(Build(items, [new SheetSet(new SetId(SetKind.Dresser, 45275), "Ao Oni-Set", [102, 100, 999, 101])]));
        Assert.Equal(new SetId(SetKind.Dresser, 45275), set.Id);
        Assert.Equal("Ao Oni-Set", set.Name);
        Assert.Equal([100u, 101u, 102u], Ids(set));
    }

    [Fact]
    public void Model_Dropped_WhenItsArmourIsInADresserSet()
    {
        var set = Assert.Single(Build([.. Asuran(), Candidate(5, "Ohrringe", Ears, set: 900)],
                                      [new SheetSet(new SetId(SetKind.Dresser, 7), "Asuran-Set", [1, 2, 3, 5])]));
        Assert.Equal(SetKind.Dresser, set.Id.Kind);
    }

    [Fact]
    public void MogStation_Dropped_WhenSameItemsAsDresser()
    {
        var items = new[] { Candidate(100, "Bikini-Oberteil", Body, set: 1), Candidate(101, "Bikini-Hose", Legs, set: 2) };
        var set = Assert.Single(Build(items, [new SheetSet(new SetId(SetKind.Dresser, 7), "Bikini-Set", [100, 101])],
                                      [new SheetSet(new SetId(SetKind.MogStation, 1000081), "Bikini", [101, 100])]));
        Assert.Equal(SetKind.Dresser, set.Id.Kind);
    }

    [Fact]
    public void MogStation_EmptyName_IsDerived()
    {
        var items = new[] { Candidate(100, "Straßen-Kappe", Head, set: 1), Candidate(101, "Straßen-Oberteil", Body, set: 2), Candidate(102, "Straßen-Schuhe", Feet, set: 3) };
        var set = Assert.Single(Build(items, mog: [new SheetSet(new SetId(SetKind.MogStation, 1000321), "", [100, 101, 102])]));
        Assert.Equal("Straßen-Serie", set.Name);
    }

    [Fact]
    public void Sheet_WithFewerThanTwoKnownPieces_IsDropped()
        => Assert.Empty(Build([Candidate(100, "Hut", Head, set: 1)], [new SheetSet(new SetId(SetKind.Dresser, 7), "Hut-Set", [100, 999])]));

    // Asuran (level 60): accessories for every role are wearable by all classes; only their names tell them apart.
    private static SetCandidate[] RoleAccessories(EquipSlot slot, uint firstId) =>
    [
        Candidate(firstId, $"Asuran-{slot} der Verteidigung", slot, set: 900, jobs: [19, 21, 23, 24]),
        Candidate(firstId + 1, $"Asuran-{slot} des Spähens", slot, set: 900, jobs: [19, 21, 23, 24]),
        Candidate(firstId + 2, $"Asuran-{slot} der Magie", slot, set: 900, jobs: [19, 21, 23, 24]),
    ];

    [Fact]
    public void Accessory_PrefersTheArmoursNameEnding()
        => Assert.Contains(11u, Ids(Assert.Single(Build([.. Asuran(), .. RoleAccessories(Ears, 10)]))));

    [Fact]
    public void Accessory_SlotStaysEmpty_WhenSeveralDifferentlyNamedOnesQualify()
    {
        var accessories = RoleAccessories(Ears, 10).Where(a => a.ItemId != 11);
        Assert.Equal([1u, 2u, 3u], Ids(Assert.Single(Build([.. Asuran(), .. accessories]))));
    }
}
