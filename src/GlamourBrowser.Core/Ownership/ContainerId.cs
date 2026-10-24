namespace GlamourBrowser.Core.Ownership;

/// <param name="Key">Retainer id in decimal for retainers, otherwise empty.</param>
public readonly record struct ContainerId(ContainerKind Kind, string Key);
