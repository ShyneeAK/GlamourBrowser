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
    /// <summary>
    /// Drops base classes whose job is in the set too (ClassJobCategory lists e.g. CNJ and WHM together),
    /// so a WHM-only item counts as one job. <paramref name="parentOf"/> is ClassJob.ClassJobParent.
    /// </summary>
    public static IReadOnlySet<uint> CollapseClasses(IReadOnlySet<uint> jobs, Func<uint, uint> parentOf)
    {
        return jobs.Where(id => !jobs.Any(job => job != id && parentOf(job) == id)).ToHashSet();
    }

    /// <summary>Gear for exactly one job (its base class may be listed too), e.g. artifact gear.</summary>
    public static bool IsSingleJob(IReadOnlySet<uint> jobs, Func<uint, uint> parentOf) => CollapseClasses(jobs, parentOf).Count == 1;

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
