using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;
using GlamourBrowser.Core.Sources;
using GlamourBrowser.Sources;

namespace GlamourBrowser.Sources;

/// <summary>
/// Builds the source index in the background at startup. The finished, immutable index is handed over
/// on the framework thread, then <see cref="Ready"/> fires.
/// </summary>
public sealed class SourceService
{
    public SourceService(IReadOnlySet<uint> catalogIds)
    {
        Task.Run(() => Build(catalogIds));
    }

    public SourceIndex Index { get; private set; } = SourceIndex.Empty;

    public bool IsReady { get; private set; }

    public event Action? Ready;

    private void Build(IReadOnlySet<uint> catalogIds)
    {
        SourceIndex index;
        var watch = Stopwatch.StartNew();
        try
        {
            index = new SourceIndexBuilder(Plugin.DataManager.GameData, catalogIds, message => Plugin.Log.Warning(message)).Build();
            Plugin.Log.Information($"Source index built in {watch.ElapsedMilliseconds} ms{(index.HasFailures ? ", failed: " + string.Join(", ", index.Failures) : "")}");
        }
        catch (Exception e)
        {
            Plugin.Log.Error(e, "Building the source index failed");
            var builder = new SourceIndex.Builder();
            builder.MarkFailure("Alle Quellen");
            index = builder.Build();
        }

        Plugin.Framework.RunOnFrameworkThread(() =>
        {
            Index = index;
            IsReady = true;
            Ready?.Invoke();
        });
    }
}
