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
    }

    public IReadOnlyList<JobInfo> All { get; }

    public JobInfo? Find(uint id) => byId.GetValueOrDefault(id);
}
