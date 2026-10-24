namespace GlamourBrowser.Core.Ui;

public enum JobBadgeKind
{
    None,
    Job,
    Role,
}

/// <param name="JobId">The job for <see cref="JobBadgeKind.Job"/>, otherwise 0.</param>
public readonly record struct JobBadge(JobBadgeKind Kind, uint JobId, JobGroup Group);

public static class JobBadges
{
    /// <summary>One job → its icon; all jobs in one role → the role icon; mixed, none or every job → no badge.</summary>
    public static JobBadge For(IReadOnlySet<uint> jobs, Func<uint, JobGroup> groupOf, int allJobsCount)
    {
        if (jobs.Count == 0 || jobs.Count >= allJobsCount)
            return default;
        if (jobs.Count == 1)
        {
            var job = jobs.First();
            return new JobBadge(JobBadgeKind.Job, job, groupOf(job));
        }

        var groups = jobs.Select(groupOf).Distinct().ToList();
        return groups.Count == 1 ? new JobBadge(JobBadgeKind.Role, 0, groups[0]) : default;
    }
}
