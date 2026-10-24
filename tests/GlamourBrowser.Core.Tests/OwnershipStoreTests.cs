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
        store.ReplaceContainer(Snap(ContainerKind.Inventory, "", "Inventar", 1, 2));
        store.ReplaceContainer(Snap(ContainerKind.Dresser, "", "Kommode", 3));
        store.ReplaceContainer(Snap(ContainerKind.Inventory, "", "Inventar", 2));

        Assert.False(store.IsOwned(1));
        Assert.True(store.IsOwned(2));
        Assert.True(store.IsOwned(3));
    }

    [Fact]
    public void LocationsOf_OneEntryPerContainer()
    {
        var store = new OwnershipStore(1);
        store.ReplaceContainer(Snap(ContainerKind.Armoire, "", "Armoire", 5));
        store.ReplaceContainer(Snap(ContainerKind.Inventory, "", "Inventar", 5, 5));

        var locations = store.LocationsOf(5);
        Assert.Equal(2, locations.Count);
        Assert.Equal(ContainerKind.Inventory, locations[0].Container.Kind);
        Assert.Equal(ContainerKind.Armoire, locations[1].Container.Kind);
    }

    [Fact]
    public void ReplaceContainer_RetainersWithSameNameStaySeparate()
    {
        var store = new OwnershipStore(1);
        store.ReplaceContainer(Snap(ContainerKind.Retainer, "11", "Gehilfe Mira", 7));
        store.ReplaceContainer(Snap(ContainerKind.Retainer, "22", "Gehilfe Mira", 7));

        Assert.Equal(2, store.LocationsOf(7).Count);
    }

    [Fact]
    public void Changed_RaisedOnReplace()
    {
        var store = new OwnershipStore(1);
        var raised = 0;
        store.Changed += () => raised++;
        store.ReplaceContainer(Snap(ContainerKind.Inventory, "", "Inventar", 1));
        Assert.Equal(1, raised);
    }
}
