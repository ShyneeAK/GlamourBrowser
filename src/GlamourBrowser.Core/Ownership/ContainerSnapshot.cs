namespace GlamourBrowser.Core.Ownership;

/// <summary>Complete contents of one container at the time it was read.</summary>
/// <param name="Name">A retainer's own name, empty for the other containers: the shown name follows the language, see <see cref="DisplayName"/>.</param>
public sealed record ContainerSnapshot(ContainerId Id, string Name, DateTimeOffset Seen, IReadOnlyList<uint> ItemIds)
{
    public string DisplayName => ContainerNames.DisplayName(Id.Kind, Name);
}
