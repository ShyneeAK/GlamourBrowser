using System.Collections.Generic;
using System.Linq;
using GlamourBrowser.Core.Rendering;
using Lumina.Excel.Sheets;

namespace GlamourBrowser.Rendering;

/// <summary>Idle pose (C-Pose index) → its looping ActionTimeline "emote/poseNN_loop", for TimelineContainer.BaseOverride.</summary>
public static class PoseTimelines
{
    private static Dictionary<string, ushort>? byKey;

    /// <returns>0 for the standard pose or when the game has no such timeline.</returns>
    public static ushort For(byte pose)
    {
        if (pose == TileKey.StandardPose)
            return 0;
        byKey ??= Plugin.DataManager.GetExcelSheet<ActionTimeline>()
            .Select(t => (Key: t.Key.ExtractText(), Id: (ushort)t.RowId))
            .Where(t => t.Key.StartsWith("emote/pose"))
            .GroupBy(t => t.Key)
            .ToDictionary(g => g.Key, g => g.First().Id);
        return byKey.GetValueOrDefault($"emote/pose{pose:D2}_loop");
    }
}
