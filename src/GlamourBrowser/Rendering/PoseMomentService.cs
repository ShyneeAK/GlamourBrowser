using System.Collections.Generic;
using System.IO;
using GlamourBrowser.Core.Rendering;

namespace GlamourBrowser.Rendering;

/// <summary>Pose moments per character, each in its own file under ConfigDirectory/pose-moments (like the camera calibration).</summary>
public sealed class PoseMomentService
{
    private readonly string directory = Path.Combine(Plugin.PluginInterface.ConfigDirectory.FullName, "pose-moments");
    private readonly Dictionary<ulong, Dictionary<Stance, float>> loaded = new();

    /// <summary>Seconds into the stance's animation; 0 (the start) when nothing was chosen.</summary>
    public float Get(ulong contentId, Stance stance) => contentId == 0 ? 0 : For(contentId).GetValueOrDefault(stance);

    public bool Has(ulong contentId, Stance stance) => contentId != 0 && For(contentId).ContainsKey(stance);

    public void Set(ulong contentId, Stance stance, float seconds)
    {
        if (contentId == 0)
            return;
        For(contentId)[stance] = seconds;
        Save(contentId);
    }

    public bool Remove(ulong contentId, Stance stance)
    {
        if (contentId == 0 || !For(contentId).Remove(stance))
            return false;
        Save(contentId);
        return true;
    }

    private Dictionary<Stance, float> For(ulong contentId)
    {
        if (!loaded.TryGetValue(contentId, out var moments))
            loaded[contentId] = moments = PoseMomentFile.Load(PathFor(contentId));
        return moments;
    }

    private void Save(ulong contentId)
    {
        try
        {
            PoseMomentFile.Save(PathFor(contentId), For(contentId));
        }
        catch (IOException e)
        {
            Plugin.Log.Warning(e, $"Saving pose moments for {contentId} failed");
        }
    }

    private string PathFor(ulong contentId) => Path.Combine(directory, $"{contentId}.json");
}
