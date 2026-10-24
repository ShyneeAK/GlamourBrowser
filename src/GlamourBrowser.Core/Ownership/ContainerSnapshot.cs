namespace GlamourBrowser.Core.Ownership;

/// <summary>Complete contents of one container at the time it was read.</summary>
public sealed record ContainerSnapshot(ContainerId Id, string DisplayName, DateTimeOffset Seen, IReadOnlyList<uint> ItemIds);
