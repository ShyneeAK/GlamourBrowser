namespace GlamourBrowser.Core.Ui;

/// <summary>A piece of the job selection with its label: a whole group ("Handwerk") or one job ("PLD").</summary>
public sealed record JobPart(string Label, IReadOnlySet<uint> Ids);

/// <summary>Choosing several jobs, or a whole group at once, in the job picker.</summary>
public static class JobSelection
{
    public static IReadOnlySet<uint> Toggle(IReadOnlySet<uint> selected, uint job)
    {
        var result = new HashSet<uint>(selected);
        if (!result.Remove(job))
            result.Add(job);
        return result;
    }

    /// <summary>Chooses every job of the group; when all of them were chosen already, none of them.</summary>
    /// <param name="jobs">All jobs (and classes) with their group.</param>
    public static IReadOnlySet<uint> ToggleGroup(IReadOnlySet<uint> selected, IReadOnlyList<(uint Id, JobGroup Group)> jobs, JobGroup group)
    {
        var ids = jobs.Where(j => j.Group == group).Select(j => j.Id).ToList();
        var result = new HashSet<uint>(selected);
        if (ids.All(selected.Contains))
            result.ExceptWith(ids);
        else
            result.UnionWith(ids);
        return result;
    }

    /// <summary>
    /// The selection in display order: a group whose jobs are all chosen as one part named after the group, other jobs
    /// one by one, ids <paramref name="jobs"/> does not know at the end.
    /// </summary>
    public static IReadOnlyList<JobPart> Parts(IReadOnlySet<uint> selected, IReadOnlyList<(uint Id, JobGroup Group)> jobs, Func<uint, string> nameOf)
    {
        var parts = new List<JobPart>();
        foreach (var group in jobs.GroupBy(j => j.Group))
        {
            var ids = group.Select(j => j.Id).ToHashSet();
            if (ids.Count > 1 && ids.All(selected.Contains))
                parts.Add(new JobPart(JobGroups.DisplayName(group.Key), ids));
            else
                parts.AddRange(group.Where(j => selected.Contains(j.Id)).Select(j => new JobPart(nameOf(j.Id), new HashSet<uint> { j.Id })));
        }
        parts.AddRange(selected.Where(id => !jobs.Any(j => j.Id == id)).Order().Select(id => new JobPart(nameOf(id), new HashSet<uint> { id })));
        return parts;
    }

    /// <summary>The job button: "Klassen/Jobs" without a choice, else the first part and how many more follow ("PLD +2").</summary>
    public static string Label(IReadOnlyList<JobPart> parts) => parts.Count switch
    {
        0 => "Klassen/Jobs",
        1 => parts[0].Label,
        _ => $"{parts[0].Label} +{parts.Count - 1}",
    };
}
