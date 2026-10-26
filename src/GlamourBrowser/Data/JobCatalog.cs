using System.Collections.Generic;
using System.Linq;
using Dalamud.Plugin.Services;
using Dalamud.Utility;
using GlamourBrowser.Core.Ui;
using Lumina.Excel.Sheets;

namespace GlamourBrowser.Data;

public sealed record JobInfo(uint Id, string Abbreviation, string Name, JobGroup Group, uint IconId);

/// <summary>All classes and jobs with their group and icon, sorted by group, then the game's UI order.</summary>
public sealed class JobCatalog
{
    private const uint JobIconBase = 62100;

    private readonly Dictionary<uint, JobInfo> byId;
    private readonly Dictionary<uint, uint> parents;

    public JobCatalog(IDataManager data)
    {
        All = data.GetExcelSheet<ClassJob>()
            .Where(j => j.RowId != 0 && !j.Abbreviation.IsEmpty)
            .Select(j => (Job: j, Info: new JobInfo(j.RowId, j.Abbreviation.ExtractText(), j.Name.ExtractText(),
                JobGroups.GroupOf(j.Role, j.ClassJobCategory.RowId, j.PrimaryStat), JobIconBase + j.RowId)))
            .OrderBy(x => x.Info.Group)
            .ThenBy(x => x.Job.UIPriority)
            .Select(x => x.Info)
            .ToList();
        byId = All.ToDictionary(j => j.Id);
        parents = data.GetExcelSheet<ClassJob>().ToDictionary(j => j.RowId, j => j.ClassJobParent.RowId);
        // A base class is the parent of another job (GLA of PLD); crafters and gatherers are their own parent only.
        var baseClasses = parents.Where(p => p.Key != p.Value).Select(p => p.Value).ToHashSet();
        Pickable = All.Where(j => !baseClasses.Contains(j.Id)).ToList();
    }

    public IReadOnlyList<JobInfo> All { get; }

    /// <summary>The jobs the job filter offers: <see cref="All"/> without the base classes (GLA, ROG, …), which a job's filter covers.</summary>
    public IReadOnlyList<JobInfo> Pickable { get; }

    public JobInfo? Find(uint id) => byId.GetValueOrDefault(id);

    /// <summary>ClassJob.ClassJobParent: a job's base class (WHM → CNJ); a class is its own parent.</summary>
    public uint ParentOf(uint id) => parents.GetValueOrDefault(id, id);

    /// <summary>The job's group; null for an id the catalog does not know.</summary>
    public JobGroup? GroupOf(uint id) => byId.TryGetValue(id, out var job) ? job.Group : null;

    /// <summary>Role icons as shown in the party list.</summary>
    public static uint RoleIcon(JobGroup group) => group switch
    {
        JobGroup.Tank => 62581,
        JobGroup.Healer => 62582,
        JobGroup.Melee => 62584,
        JobGroup.Ranged => 62586,
        JobGroup.Magic => 62587,
        JobGroup.Crafter => 62588,
        _ => 62589,
    };
}
