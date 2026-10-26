using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using GlamourBrowser.Core.Sources;
using GlamourBrowser.Sources;
using GlamourBrowser.Core.Localization;

namespace GlamourBrowser.Sources;

/// <summary>
/// Builds the source index in the background at startup. The finished, immutable index is handed over
/// on the framework thread, then <see cref="Ready"/> fires.
/// </summary>
public sealed class SourceService : IDisposable
{
    private readonly CancellationTokenSource cancel = new();
    private readonly Task build;

    public SourceService(IReadOnlySet<uint> catalogIds)
    {
        build = Task.Run(() => Build(catalogIds, cancel.Token));
    }

    public SourceIndex Index { get; private set; } = SourceIndex.Empty;

    public bool IsReady { get; private set; }

    public event Action? Ready;

    private void Build(IReadOnlySet<uint> catalogIds, CancellationToken token)
    {
        SourceIndex index;
        var watch = Stopwatch.StartNew();
        try
        {
            index = new SourceIndexBuilder(Plugin.DataManager.GameData, catalogIds, message => Plugin.Log.Warning(message), token).Build();
            Plugin.Log.Information($"Source index built in {watch.ElapsedMilliseconds} ms{(index.HasFailures ? ", failed: " + string.Join(", ", index.Failures) : "")}");
        }
        catch (OperationCanceledException)
        {
            return;
        }
        catch (Exception e)
        {
            Plugin.Log.Error(e, "Building the source index failed");
            var builder = new SourceIndex.Builder();
            builder.MarkFailure(Loc.T("Alle Quellen"));
            index = builder.Build();
        }

        Plugin.Framework.RunOnFrameworkThread(() =>
        {
            // The plugin may have been unloaded before the framework got to the hand-over.
            if (token.IsCancellationRequested)
                return;
            Index = index;
            IsReady = true;
            Ready?.Invoke();
        });
    }

    /// <summary>Stops a running build and waits for it, so no source code runs once the plugin is unloaded.</summary>
    public void Dispose()
    {
        cancel.Cancel();
        Ready = null;
        build.Wait();
        cancel.Dispose();
    }
}
