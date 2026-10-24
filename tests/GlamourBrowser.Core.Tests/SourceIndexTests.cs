using GlamourBrowser.Core.Sources;

namespace GlamourBrowser.Core.Tests;

public class SourceIndexTests
{
    private static ItemSource Src(SourceCategory c, string title = "T", string detail = "D") => new(c, title, detail);

    [Fact]
    public void Item_WithoutSources_IsOther()
        => Assert.Equal(new HashSet<SourceCategory> { SourceCategory.Other }, SourceIndex.Empty.CategoriesOf(5));

    [Fact]
    public void MultipleCategoriesPerItem()
    {
        var b = new SourceIndex.Builder();
        b.Add(1, Src(SourceCategory.Crafting));
        b.Add(1, Src(SourceCategory.GilVendor));
        Assert.Equal(new HashSet<SourceCategory> { SourceCategory.Crafting, SourceCategory.GilVendor }, b.Build().CategoriesOf(1));
    }

    [Fact]
    public void DuplicateSourcesAreIgnored()
    {
        var b = new SourceIndex.Builder();
        b.Add(1, Src(SourceCategory.RaidSavage, "Raid", "Boss 1"));
        b.Add(1, Src(SourceCategory.RaidSavage, "Raid", "Boss 1"));
        Assert.Single(b.Build().SourcesOf(1));
    }

    [Fact]
    public void Counts_CountEachItemOncePerCategory()
    {
        var b = new SourceIndex.Builder();
        b.Add(1, Src(SourceCategory.GilVendor, "A"));
        b.Add(1, Src(SourceCategory.GilVendor, "B"));
        b.Add(2, Src(SourceCategory.GilVendor, "C"));
        var counts = b.Build().Counts([1, 2, 3]);

        Assert.Equal(2, counts[SourceCategory.GilVendor]);
        Assert.Equal(1, counts[SourceCategory.Other]);
    }

    [Fact]
    public void PatchOf_UnknownIsZero()
    {
        var b = new SourceIndex.Builder();
        b.SetPatch(1, 6.4f);
        var index = b.Build();
        Assert.Equal(6.4f, index.PatchOf(1));
        Assert.Equal(0f, index.PatchOf(2));
    }

    [Fact]
    public void Builder_MarkFailure_KeepsOtherSources()
    {
        var b = new SourceIndex.Builder();
        b.Add(1, Src(SourceCategory.RaidSavage));
        b.MarkFailure("Quests");
        var index = b.Build();

        Assert.True(index.HasFailures);
        Assert.Equal(["Quests"], index.Failures);
        Assert.Equal(new HashSet<SourceCategory> { SourceCategory.RaidSavage }, index.CategoriesOf(1));
    }

    [Fact]
    public void Build_ReturnsSnapshot()
    {
        var b = new SourceIndex.Builder();
        b.Add(1, Src(SourceCategory.RaidSavage));
        var index = b.Build();
        b.Add(1, Src(SourceCategory.PvP));
        Assert.Single(index.SourcesOf(1));
    }

    [Fact]
    public void KnownCategoriesOf_EmptyWithoutSources()
    {
        var b = new SourceIndex.Builder();
        b.Add(1, Src(SourceCategory.RaidSavage));
        var index = b.Build();
        Assert.Empty(index.KnownCategoriesOf(5));
        Assert.Equal(new HashSet<SourceCategory> { SourceCategory.Other }, index.CategoriesOf(5));
        Assert.Equal(new HashSet<SourceCategory> { SourceCategory.RaidSavage }, index.KnownCategoriesOf(1));
    }
}
