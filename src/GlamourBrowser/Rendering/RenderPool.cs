using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Numerics;
using System.Threading.Tasks;
using Dalamud.Interface.Textures;
using Dalamud.Interface.Textures.TextureWraps;
using GlamourBrowser.Core.Items;
using GlamourBrowser.Core.Rendering;

namespace GlamourBrowser.Rendering;

/// <summary>
/// Renders queued tiles with up to three CharaViews (slots 7, 6, 5) and puts the results into the cache.
/// Everything runs on the framework thread via <see cref="Tick"/>.
/// </summary>
public sealed unsafe class RenderPool : IDisposable
{
    private static readonly uint[] Slots = [7, 6, 5];
    private const int SettleFrames = 1;
    private const int TimeoutFrames = 600;
    private static readonly TimeSpan IdleRelease = TimeSpan.FromSeconds(5);

    private enum Phase { WaitReady, Apply, Loading, Settle, Copy }

    private sealed class Worker(CharaViewHandle view)
    {
        public CharaViewHandle View { get; } = view;
        public Phase Phase { get; set; } = Phase.WaitReady;
        public TileKey? Key { get; set; }
        public int Frames { get; set; }
        public Stopwatch Stopwatch { get; } = new();
        public Task<IDalamudTextureWrap>? CopyTask { get; set; }
        public (TileMode Mode, ulong Hash)? DressedAs { get; set; }
        public bool Busy => Key != null;
    }

    private readonly TileQueue queue;
    private readonly TileCache<IDalamudTextureWrap> cache;
    private readonly Func<uint, GlamourItem?> lookup;
    private readonly AppearanceTracker appearance;
    private readonly Func<EquipSlot, CameraPreset?> cameraFor;
    private readonly List<Worker> workers = new();
    private readonly Dictionary<TileKey, string> failures = new();
    private DateTime lastWork = DateTime.UtcNow;
    private bool liveReserved;

    public RenderPool(TileQueue queue, TileCache<IDalamudTextureWrap> cache, Func<uint, GlamourItem?> lookup,
                      AppearanceTracker appearance, Func<EquipSlot, CameraPreset?> cameraFor)
    {
        this.queue = queue;
        this.cache = cache;
        this.lookup = lookup;
        this.appearance = appearance;
        this.cameraFor = cameraFor;
    }

    public int MaxViews { get; set; } = 3;
    public bool IsPaused { get; private set; }
    public IReadOnlyDictionary<TileKey, string> Failures => failures;

    /// <summary>True while the live preview may take slot 7 (no worker on it).</summary>
    public bool LiveSlotFree => liveReserved && workers.All(w => w.View.Index != Slots[0]);

    public event Action<TileKey>? TileReady;

    /// <summary>Raised when in-flight tiles were dropped (pause, slot conflict); requesters should re-request.</summary>
    public event Action? Disrupted;

    public void ReserveLiveSlot(bool reserved) => liveReserved = reserved;

    public void Pause()
    {
        IsPaused = true;
        ReleaseAll();
    }

    public void Resume() => IsPaused = false;

    public void ClearFailures() => failures.Clear();

    public void Tick()
    {
        if (IsPaused)
            return;

        if (SlotGuard.ConflictActive())
        {
            if (workers.Count > 0)
            {
                foreach (var worker in workers)
                    worker.View.Abandon();
                workers.Clear();
                Disrupted?.Invoke();
            }
            return;
        }

        ManageWorkers();
        foreach (var worker in workers)
        {
            worker.View.Tick();
            Step(worker);
        }
    }

    private void ManageWorkers()
    {
        var wanted = Slots.Take(Math.Clamp(MaxViews, 1, Slots.Length)).Where(s => !(liveReserved && s == Slots[0])).ToList();

        // Hand slot 7 to the live preview once its worker finished the current tile.
        foreach (var worker in workers.Where(w => !wanted.Contains(w.View.Index) && !w.Busy).ToList())
            Release(worker);

        if (queue.Count > 0 || workers.Any(w => w.Busy))
        {
            lastWork = DateTime.UtcNow;
            var character = AppearanceTracker.LocalCharacter();
            if (character == null || !appearance.IsValid)
                return;

            foreach (var slot in wanted.Where(s => workers.All(w => w.View.Index != s)))
            {
                if (!CharaViewHandle.IsSlotFree(slot))
                    continue;
                var view = new CharaViewHandle(slot);
                view.Create(character, appearance.Race, appearance.Sex);
                workers.Add(new Worker(view));
            }
        }
        else if (workers.Count > 0 && DateTime.UtcNow - lastWork > IdleRelease)
        {
            ReleaseAll();
        }
    }

    private void Step(Worker worker)
    {
        var view = worker.View;
        switch (worker.Phase)
        {
            case Phase.WaitReady:
                if (view.IsIdle)
                    worker.Phase = Phase.Apply;
                break;

            case Phase.Apply:
                if (!view.IsIdle || !queue.TryDequeue(out var key, out _))
                    return;
                if (lookup(key.ItemId) is not { } item)
                {
                    failures[key] = "Unbekanntes Item";
                    return;
                }

                worker.Key = key;
                worker.Stopwatch.Restart();
                if (worker.DressedAs != (key.Mode, key.AppearanceHash))
                {
                    view.ApplyOutfit(appearance.Outfit, key.Mode, item.Slot);
                    worker.DressedAs = (key.Mode, key.AppearanceHash);
                }
                view.SetItem(item.Slot, key.ItemId, key.Stain0, key.Stain1);
                // Tiles keep the weapon sheathed: drawing it plays an animation that would have to finish first.
                view.ApplyCamera(cameraFor(item.Slot));
                worker.Frames = 0;
                worker.Phase = Phase.Loading;
                break;

            case Phase.Loading:
                worker.Frames++;
                // Give SetItemSlotData a frame to raise its flags, then wait until both are clear.
                if (worker.Frames < 2 || !view.IsIdle)
                {
                    if (worker.Frames > TimeoutFrames)
                        Fail(worker, "Zeitüberschreitung beim Laden");
                    return;
                }
                worker.Frames = 0;
                worker.Phase = Phase.Settle;
                break;

            case Phase.Settle:
                if (++worker.Frames < SettleFrames)
                    return;
                var texture = view.GetTexture();
                if (texture == null || texture->D3D11ShaderResourceView == null)
                {
                    Fail(worker, "Keine Textur");
                    return;
                }
                var width = worker.Key!.Value.RenderWidth;
                var args = new TextureModificationArgs
                {
                    Uv1 = new Vector2((float)texture->ActualWidth / texture->AllocatedWidth, (float)texture->ActualHeight / texture->AllocatedHeight),
                    NewWidth = width,
                    NewHeight = width * 960 / 576,
                };
                worker.CopyTask = Plugin.TextureProvider.CreateFromExistingTextureAsync(new GameTextureWrap(texture), args, leaveWrapOpen: true, debugName: $"GlamourBrowser tile {worker.Key.Value.ItemId}");
                worker.Phase = Phase.Copy;
                break;

            case Phase.Copy:
                if (worker.CopyTask is not { IsCompleted: true } task)
                    return;
                worker.CopyTask = null;
                if (!task.IsCompletedSuccessfully)
                {
                    Fail(worker, task.Exception?.InnerException?.Message ?? "Kopieren fehlgeschlagen");
                    return;
                }

                var done = worker.Key!.Value;
                cache.Add(done, task.Result);
                failures.Remove(done);
                Plugin.Log.Verbose($"Tile {done.ItemId} on slot {view.Index}: {worker.Stopwatch.Elapsed.TotalMilliseconds:F0} ms");
                Finish(worker);
                TileReady?.Invoke(done);
                break;
        }
    }

    private void Fail(Worker worker, string error)
    {
        var key = worker.Key!.Value;
        failures[key] = error;
        Plugin.Log.Warning($"Tile {key.ItemId} failed on slot {worker.View.Index}: {error}");
        Finish(worker);
        TileReady?.Invoke(key);
    }

    private static void Finish(Worker worker)
    {
        worker.Key = null;
        worker.Phase = Phase.Apply;
    }

    private void Release(Worker worker)
    {
        worker.View.Dispose();
        workers.Remove(worker);
    }

    private void ReleaseAll()
    {
        var hadWork = workers.Any(w => w.Busy);
        foreach (var worker in workers)
            worker.View.Dispose();
        workers.Clear();
        if (hadWork)
            Disrupted?.Invoke();
    }

    public void Dispose() => ReleaseAll();
}
