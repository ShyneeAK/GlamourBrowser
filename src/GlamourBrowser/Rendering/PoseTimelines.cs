using System.Collections.Generic;
using System.Linq;
using GlamourBrowser.Core.Rendering;
using Lumina.Excel.Sheets;

namespace GlamourBrowser.Rendering;

/// <summary>Idle pose (C-Pose index) → its looping ActionTimeline "emote/poseNN_loop", for TimelineContainer.BaseOverride.</summary>
public static class PoseTimelines
{
    /// <summary>ActionTimeline "normal/idle": the standard pose, played to end a pose loop.</summary>
    public const ushort StandardIdle = 3;

    private static Dictionary<string, ushort>? byKey;
    private static HashSet<ushort>? poses;

    /// <returns>0 for the standard pose or when the game has no such timeline.</returns>
    public static ushort For(byte pose)
    {
        if (pose == TileKey.StandardPose)
            return 0;
        return ByKey().GetValueOrDefault($"emote/pose{pose:D2}_loop");
    }

    /// <summary>Whether the timeline is one of the idle poses ("emote/pose…", start, loop or centered).</summary>
    public static bool IsPose(ushort timeline)
    {
        if (timeline == 0)
            return false;
        ByKey();
        return poses!.Contains(timeline);
    }

    private static Dictionary<string, ushort> ByKey()
    {
        if (byKey != null)
            return byKey;
        var timelines = Plugin.DataManager.GetExcelSheet<ActionTimeline>()
            .Select(t => (Key: t.Key.ExtractText(), Id: (ushort)t.RowId))
            .Where(t => t.Key.StartsWith("emote/pose"))
            .ToList();
        poses = timelines.Select(t => t.Id).ToHashSet();
        return byKey = timelines.GroupBy(t => t.Key).ToDictionary(g => g.Key, g => g.First().Id);
    }
}
