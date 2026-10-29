namespace GlamourBrowser.Core.Ownership;

/// <summary>Last known contents of every container of one character.</summary>
/// <param name="variants">"+N" variants mapped to their base item (see <see cref="Items.ItemVariants"/>): a variant counts for its base too.</param>
public sealed class OwnershipStore(ulong contentId, IReadOnlyDictionary<uint, uint>? variants = null)
{
    private readonly Dictionary<ContainerId, ContainerSnapshot> containers = new();
    private readonly Dictionary<uint, List<ContainerId>> index = new();

    public ulong ContentId { get; } = contentId;

    public IReadOnlyCollection<ContainerSnapshot> Containers => containers.Values;

    public event Action? Changed;

    /// <summary>
    /// Replaces everything previously known about this container. Returns false (and stays quiet) when
    /// only the read time moved: same name and the same items, in any order, and the same dresser entries.
    /// </summary>
    public bool ReplaceContainer(ContainerSnapshot snapshot)
    {
        var changed = !containers.TryGetValue(snapshot.Id, out var old) || !SameContent(old, snapshot);
        containers[snapshot.Id] = snapshot;
        if (!changed)
            return false;

        RebuildIndex();
        Changed?.Invoke();
        return true;
    }

    private static bool SameContent(ContainerSnapshot a, ContainerSnapshot b)
        => a.Name == b.Name && a.ItemIds.Order().SequenceEqual(b.ItemIds.Order()) && a.Entries.SequenceEqual(b.Entries);

    public bool IsOwned(uint itemId) => index.ContainsKey(itemId);

    public IReadOnlyList<ItemLocation> LocationsOf(uint itemId)
    {
        if (!index.TryGetValue(itemId, out var ids))
            return [];

        return ids
            .Select(id => containers[id])
            .OrderBy(c => c.Id.Kind)
            .ThenBy(c => c.DisplayName, StringComparer.Ordinal)
            .Select(c => new ItemLocation(c.Id, c.Name, c.Seen))
            .ToList();
    }

    private void Record(uint itemId, ContainerId container)
    {
        if (!index.TryGetValue(itemId, out var list))
            index[itemId] = list = new List<ContainerId>();
        // A container holding the base item and a variant is still one place.
        if (!list.Contains(container))
            list.Add(container);
    }

    private void RebuildIndex()
    {
        index.Clear();
        foreach (var container in containers.Values)
        {
            foreach (var itemId in container.ItemIds.Distinct())
            {
                Record(itemId, container.Id);
                if (variants != null && variants.TryGetValue(itemId, out var baseId))
                    Record(baseId, container.Id);
            }
        }
    }
}
