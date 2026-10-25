namespace GlamourBrowser.Core.Items;

/// <summary>
/// The job filter. Every job can wear everything as glamour, so it looks for gear made for the chosen jobs: at least one
/// of them and no other job may be listed, base classes of chosen jobs aside ("DRK", "GLA PLD", all crafters for
/// all crafters chosen), not gear that other jobs or everyone can wear.
/// </summary>
/// <param name="parentOf">ClassJob.ClassJobParent: a job's base class; a class is its own parent.</param>
public sealed class JobFilter(IReadOnlySet<uint> chosen, Func<uint, uint> parentOf)
{
    private readonly HashSet<uint> allowed = chosen.Concat(chosen.Select(parentOf)).ToHashSet();

    public bool Fits(IReadOnlySet<uint> itemJobs) => itemJobs.Any(chosen.Contains) && itemJobs.All(allowed.Contains);
}
