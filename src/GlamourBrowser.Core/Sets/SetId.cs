namespace GlamourBrowser.Core.Sets;

public enum SetKind
{
    None,
    Dresser,
    MogStation,
    Model,
}

/// <summary>
/// Stable id of a set: the dresser set item, the mog station row, or the lowest item id of a model series.
/// The default value means "no set".
/// </summary>
public readonly record struct SetId(SetKind Kind, uint Value)
{
    public bool IsSet => Kind != SetKind.None;
}
