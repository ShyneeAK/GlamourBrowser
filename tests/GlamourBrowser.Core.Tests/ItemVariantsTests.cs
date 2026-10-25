using GlamourBrowser.Core.Items;

namespace GlamourBrowser.Core.Tests;

public class ItemVariantsTests
{
    private static VariantCandidate Item(uint id, string name, ulong model = 100, ulong sub = 0) => new(id, name, model, sub);

    [Fact]
    public void MergesPlusVariantsThatLookTheSame()
        => Assert.Equal(new Dictionary<uint, uint> { [2] = 1, [3] = 1 },
                        ItemVariants.BaseOf([Item(1, "Galatyn"), Item(2, "Galatyn +1"), Item(3, "Galatyn +2")]));

    [Fact]
    public void KeepsVariantsThatLookDifferent()
        => Assert.Empty(ItemVariants.BaseOf([Item(1, "Elementar-Helm der Verteidigung", model: 100), Item(2, "Elementar-Helm der Verteidigung +1", model: 300)]));

    [Fact]
    public void ComparesTheSecondModelToo()
        => Assert.Empty(ItemVariants.BaseOf([Item(1, "Schild", sub: 5), Item(2, "Schild +1", sub: 6)]));

    [Fact]
    public void MergesOnlyTheVariantsWithTheBaseModel()
        => Assert.Equal(new Dictionary<uint, uint> { [2] = 1 },
                        ItemVariants.BaseOf([Item(1, "Helm"), Item(2, "Helm +1"), Item(3, "Helm +2", model: 300)]));

    [Fact]
    public void WithoutABaseItem_TheLowestIdIsTheBase()
        => Assert.Equal(new Dictionary<uint, uint> { [8] = 7 }, ItemVariants.BaseOf([Item(8, "Hut +2"), Item(7, "Hut +1")]));

    [Fact]
    public void OtherNamesStayAlone()
        => Assert.Empty(ItemVariants.BaseOf([Item(1, "Ring +1 der Stärke"), Item(2, "Ring der Stärke"), Item(3, "Hut")]));
}
