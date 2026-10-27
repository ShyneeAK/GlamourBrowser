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
/// Builds the source index in the background at startup, and again after a language change (its texts, such as
/// "Boss 1", are built in). The finished, immutable index is handed over on the framework thread, then
/// <see cref="Ready"/> fires; until then the previous index stays.
/// </summary>
public sealed class SourceService : IDisposable
{
    private readonly IReadOnlySet<uint> catalogIds;
    private CancellationTokenSource cancel = new();
    private Task build;

    public SourceService(IReadOnlySet<uint> catalogIds)
    {
        this.catalogIds = catalogIds;
        var token = cancel.Token;
        build = Task.Run(() => Build(token));
        Loc.Loaded += Rebuild;
    }

    public SourceIndex Index { get; private set; } = SourceIndex.Empty;

    public bool IsReady { get; private set; }

    /// <summary>Counts the hand-overs: texts taken from an older index are stale once it moved.</summary>
    public int Version { get; private set; }

    public event Action? Ready;

    /// <summary>Stops a running build (it would mix both languages) and starts a new one once it has stopped.</summary>
    private void Rebuild()
    {
        var previous = cancel;
        previous.Cancel();
        cancel = new CancellationTokenSource();
        var token = cancel.Token;
        build = build.ContinueWith(_ =>
        {
            previous.Dispose();
            Build(token);
        }, TaskScheduler.Default);
    }

    private void Build(CancellationToken token)
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
            // The plugin may have been unloaded, or the language changed, before the framework got to the hand-over.
            if (token.IsCancellationRequested)
                return;
            Index = index;
            IsReady = true;
            Version++;
            Ready?.Invoke();
        });
    }

    /// <summary>Stops a running build and waits for it, so no source code runs once the plugin is unloaded.</summary>
    public void Dispose()
    {
        Loc.Loaded -= Rebuild;
        cancel.Cancel();
        Ready = null;
        build.Wait();
        cancel.Dispose();
    }
}
