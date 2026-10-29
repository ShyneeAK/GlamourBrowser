namespace GlamourBrowser.Core.Ownership;

/// <summary>One glamour dresser entry: its raw id (HQ above <see cref="DresserContents.HqOffset"/>, or an outfit) and the dyes it is stored with.</summary>
public readonly record struct DresserEntry(uint RawId, byte Stain0, byte Stain1);

/// <summary>Complete contents of one container at the time it was read.</summary>
/// <param name="Name">A retainer's own name, empty for the other containers: the shown name follows the language, see <see cref="DisplayName"/>.</param>
public sealed record ContainerSnapshot(ContainerId Id, string Name, DateTimeOffset Seen, IReadOnlyList<uint> ItemIds)
{
    /// <summary>The glamour dresser's entries as the game holds them, with their dyes (other containers: none).</summary>
    public IReadOnlyList<DresserEntry> Entries { get; init; } = [];

    public string DisplayName => ContainerNames.DisplayName(Id.Kind, Name);
}
