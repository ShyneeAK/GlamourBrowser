using GlamourBrowser.Core.Items;

namespace GlamourBrowser.Core.Sets;

/// <summary>How many pieces of a set are owned ("3/5").</summary>
public readonly record struct SetProgress(int Owned, int Total)
{
    public bool Complete => Total > 0 && Owned == Total;

    public static SetProgress Of(SetSubject set, Func<uint, bool> isOwned) => new(set.Pieces.Count(p => isOwned(p.Id)), set.Pieces.Count);
}
