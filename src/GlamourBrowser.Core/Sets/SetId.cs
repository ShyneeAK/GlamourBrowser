namespace GlamourBrowser.Core.Sets;

public enum SetKind
{
    None,
    Dresser,
    MogStation,
    Model,
    /// <summary>A set the player built (see <see cref="CustomSets.CustomSet"/>); the value is its id.</summary>
    Custom,
}

/// <summary>
/// Stable id of a set: the dresser set item, the mog station row, the lowest armour item id of a model series, or the
/// id of a custom set.
/// The default value means "no set".
/// </summary>
public readonly record struct SetId(SetKind Kind, uint Value)
{
    public bool IsSet => Kind != SetKind.None;
}
