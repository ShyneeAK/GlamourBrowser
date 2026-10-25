namespace GlamourBrowser.Core.Items;

/// <summary>
/// The job filter. Every job can wear everything as glamour, so it looks for gear made for that one job: only the job
/// and its base class may be listed ("DRK", "GLA PLD", "CRP"), not gear for several jobs or for everyone.
/// </summary>
public static class JobFilter
{
    /// <param name="parentOf">ClassJob.ClassJobParent: a job's base class; a class is its own parent.</param>
    public static bool OnlyFor(IReadOnlySet<uint> jobs, uint job, Func<uint, uint> parentOf)
    {
        if (!jobs.Contains(job))
            return false;
        var baseClass = parentOf(job);
        return jobs.All(j => j == job || j == baseClass);
    }
}
