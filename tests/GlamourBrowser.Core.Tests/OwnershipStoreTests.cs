using GlamourBrowser.Core.Ownership;

namespace GlamourBrowser.Core.Tests;

public class OwnershipStoreTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 24, 13, 40, 0, TimeSpan.FromHours(2));

    private static ContainerSnapshot Snap(ContainerKind kind, string key, string name, params uint[] ids)
        => new(new ContainerId(kind, key), name, Now, ids);

    [Fact]
    public void ReplaceContainer_ReplacesOnlyThatContainer()
    {
        var store = new OwnershipStore(1);
        store.ReplaceContainer(Snap(ContainerKind.Inventory, "", "", 1, 2));
        store.ReplaceContainer(Snap(ContainerKind.Dresser, "", "", 3));
        store.ReplaceContainer(Snap(ContainerKind.Inventory, "", "", 2));

        Assert.False(store.IsOwned(1));
        Assert.True(store.IsOwned(2));
        Assert.True(store.IsOwned(3));
    }

    [Fact]
    public void ReplaceContainer_ANewDyeInTheDresserIsAChange()
    {
        var store = new OwnershipStore(1);
        var changes = 0;
        store.Changed += () => changes++;
        store.ReplaceContainer(Snap(ContainerKind.Dresser, "", "", 10) with { Entries = [new DresserEntry(10, 5, 0)] });
        store.ReplaceContainer(Snap(ContainerKind.Dresser, "", "", 10) with { Entries = [new DresserEntry(10, 5, 0)] });
        store.ReplaceContainer(Snap(ContainerKind.Dresser, "", "", 10) with { Entries = [new DresserEntry(10, 6, 0)] });

        Assert.Equal(2, changes);
    }

    [Fact]
    public void LocationsOf_OneEntryPerContainer()
    {
        var store = new OwnershipStore(1);
        store.ReplaceContainer(Snap(ContainerKind.Armoire, "", "", 5));
        store.ReplaceContainer(Snap(ContainerKind.Inventory, "", "", 5, 5));

        var locations = store.LocationsOf(5);
        Assert.Equal(2, locations.Count);
        Assert.Equal(ContainerKind.Inventory, locations[0].Container.Kind);
        Assert.Equal(ContainerKind.Armoire, locations[1].Container.Kind);
    }

    [Fact]
    public void ReplaceContainer_RetainersWithSameNameStaySeparate()
    {
        var store = new OwnershipStore(1);
        store.ReplaceContainer(Snap(ContainerKind.Retainer, "11", "Mira", 7));
        store.ReplaceContainer(Snap(ContainerKind.Retainer, "22", "Mira", 7));

        Assert.Equal(2, store.LocationsOf(7).Count);
    }

    [Fact]
    public void ReplaceContainer_SameItemsInOtherOrder_IsUnchangedAndQuiet()
    {
        var store = new OwnershipStore(1);
        Assert.True(store.ReplaceContainer(Snap(ContainerKind.Inventory, "", "", 1, 2, 2)));
        var raised = 0;
        store.Changed += () => raised++;

        var later = new ContainerSnapshot(new ContainerId(ContainerKind.Inventory, ""), "", Now.AddHours(1), [2, 1, 2]);
        Assert.False(store.ReplaceContainer(later));
        Assert.Equal(0, raised);
        Assert.Equal(Now.AddHours(1), store.Containers.Single().Seen);
    }

    [Fact]
    public void ReplaceContainer_DifferentItems_IsChanged()
    {
        var store = new OwnershipStore(1);
        store.ReplaceContainer(Snap(ContainerKind.Inventory, "", "", 1, 2));
        Assert.True(store.ReplaceContainer(Snap(ContainerKind.Inventory, "", "", 1, 3)));
    }

    [Fact]
    public void ReplaceContainer_RenamedRetainer_IsChanged()
    {
        var store = new OwnershipStore(1);
        store.ReplaceContainer(Snap(ContainerKind.Retainer, "11", "Mira", 7));
        Assert.True(store.ReplaceContainer(Snap(ContainerKind.Retainer, "11", "Kai", 7)));
    }

    [Fact]
    public void Changed_RaisedOnReplace()
    {
        var store = new OwnershipStore(1);
        var raised = 0;
        store.Changed += () => raised++;
        store.ReplaceContainer(Snap(ContainerKind.Inventory, "", "", 1));
        Assert.Equal(1, raised);
    }

    [Fact]
    public void Variant_CountsForItsBase()
    {
        var store = new OwnershipStore(1, new Dictionary<uint, uint> { [12] = 10 });
        store.ReplaceContainer(Snap(ContainerKind.Dresser, "", "", 12));
        Assert.True(store.IsOwned(10));
        Assert.True(store.IsOwned(12));
        Assert.Equal("Kommode", Assert.Single(store.LocationsOf(10)).DisplayName);
    }

    [Fact]
    public void Location_NamesTheContainerInTheLoadedLanguage()
    {
        var store = new OwnershipStore(1);
        store.ReplaceContainer(Snap(ContainerKind.Retainer, "11", "Mira", 7));
        store.ReplaceContainer(Snap(ContainerKind.PremiumSaddlebag, "", "", 7));

        // Without a loaded language the texts stay German.
        Assert.Equal(["Satteltasche (Premium)", "Gehilfe Mira"], store.LocationsOf(7).Select(l => l.DisplayName));
    }
}
