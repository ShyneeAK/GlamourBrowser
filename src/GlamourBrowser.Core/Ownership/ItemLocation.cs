namespace GlamourBrowser.Core.Ownership;

/// <param name="Name">A retainer's own name, empty for the other containers, see <see cref="ContainerSnapshot"/>.</param>
public sealed record ItemLocation(ContainerId Container, string Name, DateTimeOffset Seen)
{
    public string DisplayName => ContainerNames.DisplayName(Container.Kind, Name);
}
