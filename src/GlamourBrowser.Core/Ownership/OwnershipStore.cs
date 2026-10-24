namespace GlamourBrowser.Core.Ownership;

/// <summary>Last known contents of every container of one character.</summary>
public sealed class OwnershipStore(ulong contentId)
{
    private readonly Dictionary<ContainerId, ContainerSnapshot> containers = new();
    private readonly Dictionary<uint, List<ContainerId>> index = new();

    public ulong ContentId { get; } = contentId;

    public IReadOnlyCollection<ContainerSnapshot> Containers => containers.Values;

    public event Action? Changed;

    /// <summary>Replaces everything previously known about this container.</summary>
    public void ReplaceContainer(ContainerSnapshot snapshot)
    {
        containers[snapshot.Id] = snapshot;
        RebuildIndex();
        Changed?.Invoke();
    }

    public bool IsOwned(uint itemId) => index.ContainsKey(itemId);

    public IReadOnlyList<ItemLocation> LocationsOf(uint itemId)
    {
        if (!index.TryGetValue(itemId, out var ids))
            return [];

        return ids
            .Select(id => containers[id])
            .OrderBy(c => c.Id.Kind)
            .ThenBy(c => c.DisplayName, StringComparer.Ordinal)
            .Select(c => new ItemLocation(c.Id, c.DisplayName, c.Seen))
            .ToList();
    }

    private void RebuildIndex()
    {
        index.Clear();
        foreach (var container in containers.Values)
        {
            foreach (var itemId in container.ItemIds.Distinct())
            {
                if (!index.TryGetValue(itemId, out var list))
                    index[itemId] = list = new List<ContainerId>();
                list.Add(container.Id);
            }
        }
    }
}
