namespace GlamourBrowser.Core.Items;

/// <summary>
/// The job filter. By default it finds gear any of the chosen jobs can wear ("GLA DRG" chosen: everything a gladiator or
/// a dragoon can put on). Exclusive, it looks for gear made for the chosen jobs: at least one of them and no other job
/// may be listed, base classes of chosen jobs aside ("DRK", "GLA PLD", all crafters for all crafters chosen), not gear
/// that other jobs or everyone can wear.
/// </summary>
/// <param name="parentOf">ClassJob.ClassJobParent: a job's base class; a class is its own parent.</param>
public sealed class JobFilter(IReadOnlySet<uint> chosen, Func<uint, uint> parentOf, bool exclusive = false)
{
    private readonly HashSet<uint> allowed = chosen.Concat(chosen.Select(parentOf)).ToHashSet();

    public bool Fits(IReadOnlySet<uint> itemJobs) => itemJobs.Any(chosen.Contains) && (!exclusive || itemJobs.All(allowed.Contains));
}
