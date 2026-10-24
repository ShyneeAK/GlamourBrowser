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
    private const int SettleFrames = 1;
    private const int TimeoutFrames = 600;
    private const int MaxVisibilityRetries = 30;
    // Frames the idle-pose timeline has to play before the copy (blend from the previous animation).
    private const int PoseBlendFrames = 12;
    private const int PoseTimeoutFrames = 180;
    // Frames the frozen moment is held (and re-applied) before the copy.
    private const int FreezeFrames = 3;
    private static readonly TimeSpan IdleRelease = TimeSpan.FromSeconds(5);

    private enum Phase { WaitReady, Apply, Loading, Settle, Pose, Freeze, Copy }

    private sealed class Worker(CharaViewHandle view)
    {
        public CharaViewHandle View { get; } = view;
        public Phase Phase { get; set; } = Phase.WaitReady;
        public TileKey? Key { get; set; }
        public int Frames { get; set; }
        public Stopwatch Stopwatch { get; } = new();
        public Task<IDalamudTextureWrap>? CopyTask { get; set; }
        public SettleGate? Gate { get; set; }
        public PoseGate? PoseGate { get; set; }
        public byte? LastPose { get; set; }
        public bool PoseChanged { get; set; }
        public bool WeaponDrawn { get; set; }
        /// <summary>The view's animation is frozen at its last frame.</summary>
        public bool Frozen { get; set; }
        public StanceGate? StanceGate { get; set; }
        public (TileMode Mode, ulong Hash, EquipSlot Slot)? DressedAs { get; set; }
        public bool Busy => Key != null;
    }

    private readonly TileQueue queue;
    private readonly TileCache<IDalamudTextureWrap> cache;
    private readonly Func<uint, GlamourItem?> lookup;
    private readonly AppearanceTracker appearance;
    private readonly Func<EquipSlot, CameraPreset?> cameraFor;
    private readonly List<Worker> workers = new();
    private readonly List<CharaViewHandle> suspended = new();
    private readonly Dictionary<TileKey, string> failures = new();
    private DateTime lastWork = DateTime.UtcNow;
    private bool liveReserved;
    private bool poseWarned;
    private int poseDiagnosticsBudget = 6;
    private bool freezeWarned;

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
    public bool LiveSlotFree => liveReserved && workers.All(w => w.View.Index != RenderSlots.LiveSlot);

    public event Action<TileKey>? TileReady;

    /// <summary>Why tiles are not rendering right now, for a hint in the grid; null when all is well.</summary>
    public string? Status { get; private set; }

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

    public System.Collections.Generic.IEnumerable<string> DescribeWeapons() => workers.Select(w => w.View.DescribeWeapons()).ToList();

    /// <summary>Diagnostics: log the weapon state of the next few finished weapon tiles.</summary>
    public int WeaponDiagnosticsBudget { get; set; }

    public void Tick()
    {
        if (IsPaused)
            return;

        if (SlotGuard.ConflictActive())
        {
            Status = "Render-Plätze werden gerade vom Spiel genutzt (Gruppen-Portraits).";
            if (workers.Count > 0)
            {
                // Set aside, not released: the game may own these slots now.
                suspended.AddRange(workers.Select(w => w.View));
                workers.Clear();
                Disrupted?.Invoke();
            }
            return;
        }

        if (suspended.Count > 0)
        {
            foreach (var view in suspended)
                view.ReleaseAfterConflict();
            suspended.Clear();
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
        var wanted = RenderSlots.Select(MaxViews, liveReserved);

        // Hand slot 7 to the live preview once its worker finished the current tile.
        foreach (var worker in workers.Where(w => !wanted.Contains(w.View.Index) && !w.Busy).ToList())
            Release(worker);

        if (queue.Count > 0 || workers.Any(w => w.Busy))
        {
            lastWork = DateTime.UtcNow;
            var character = AppearanceTracker.LocalCharacter();
            if (character == null || !appearance.IsValid)
                return;

            var blocked = 0;
            foreach (var slot in wanted.Where(s => workers.All(w => w.View.Index != s)))
            {
                if (!CharaViewHandle.IsSlotFree(slot))
                {
                    blocked++;
                    continue;
                }
                var view = new CharaViewHandle(slot);
                view.Create(character, appearance.Race, appearance.Sex);
                workers.Add(new Worker(view));
            }
            Status = workers.Count == 0 && blocked > 0 ? "Kein Render-Platz frei (Plätze 5–7 belegt)." : null;
        }
        else
        {
            Status = null;
            if (workers.Count > 0 && DateTime.UtcNow - lastWork > IdleRelease)
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
                // Re-dress when the slot changes too, otherwise the previous tile's item (e.g. a hat)
                // stays on the character.
                var redressed = worker.DressedAs != (key.Mode, key.AppearanceHash, item.Slot);
                if (redressed)
                {
                    view.ApplyOutfit(appearance.Outfit, key.Mode, item.Slot);
                    worker.DressedAs = (key.Mode, key.AppearanceHash, item.Slot);
                }
                view.SetItem(item.Slot, key.ItemId, key.Stain0, key.Stain1);
                // Weapon tiles may draw the weapon: the draw animation has to finish first (StanceGate in the Pose phase).
                view.ApplyCamera(cameraFor(item.Slot));
                // A view keeps its pose and frozen moment from tile to tile; only a change (or a re-dress, which may
                // rebuild the character) lets the animation run again and waits for it in the Pose phase.
                var weaponChanged = worker.WeaponDrawn != key.WeaponDrawn;
                worker.PoseChanged = redressed || weaponChanged || worker.LastPose != key.Pose;
                worker.LastPose = key.Pose;
                if (worker.PoseChanged)
                {
                    view.SetAnimationPaused(false);
                    worker.Frozen = false;
                }
                if (!view.ApplyIdlePose(key.Pose) && !poseWarned)
                {
                    // The tile then renders in the standard pose.
                    poseWarned = true;
                    Plugin.Log.Warning($"Idle pose {key.Pose} could not be applied; tiles use the standard pose");
                }
                worker.StanceGate = null;
                if (key.WeaponDrawn || worker.WeaponDrawn)
                {
                    if (weaponChanged)
                        view.SetWeaponDrawn(key.WeaponDrawn);
                    worker.WeaponDrawn = key.WeaponDrawn;
                    if (key.WeaponDrawn)
                    {
                        // The timeline playing before the draw is the reference the gate waits to see replaced.
                        worker.StanceGate = new StanceGate(worker.PoseChanged ? PoseBlendFrames : 0, PoseTimeoutFrames, expectChange: weaponChanged);
                        worker.StanceGate.Step(view.BaseTimeline().Playing);
                    }
                }
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
                // A freshly loaded weapon can come back sheathed even though the view had it drawn: check the
                // character itself, not what this worker did last time, and draw (or sheathe) again if needed.
                if (view.IsWeaponDrawn() != worker.Key!.Value.WeaponDrawn)
                {
                    if (!worker.PoseChanged)
                    {
                        worker.PoseChanged = true;
                        view.SetAnimationPaused(false);
                        worker.Frozen = false;
                    }
                    view.SetWeaponDrawn(worker.Key.Value.WeaponDrawn);
                    worker.WeaponDrawn = worker.Key.Value.WeaponDrawn;
                    if (worker.Key.Value.WeaponDrawn)
                    {
                        worker.StanceGate = new StanceGate(PoseBlendFrames, PoseTimeoutFrames, expectChange: true);
                        worker.StanceGate.Step(view.BaseTimeline().Playing);
                    }
                    else
                    {
                        worker.StanceGate = null;
                    }
                }
                view.ApplyVisibility(showWeapons: true);
                worker.Gate = new SettleGate(SettleFrames, TimeoutFrames, MaxVisibilityRetries);
                worker.Phase = Phase.Settle;
                break;

            case Phase.Settle:
                // ApplyVisibility may start another short load, and a weapon that finished loading late
                // comes back hidden; the gate re-applies a bounded number of times.
                switch (worker.Gate!.Step(view.IsIdle, view.WeaponsShown()))
                {
                    case SettleAction.Wait:
                        return;
                    case SettleAction.Reapply:
                        view.ApplyVisibility(showWeapons: true);
                        return;
                    case SettleAction.Timeout:
                        Fail(worker, "Zeitüberschreitung beim Laden");
                        return;
                    case SettleAction.CopyWithoutWeapons:
                        Plugin.Log.Warning($"Tile {worker.Key!.Value.ItemId}: weapons still hidden after {worker.Gate.Retries} retries, copying anyway");
                        break;
                }
                if (worker.Gate.Retries > 0)
                    Plugin.Log.Verbose($"Tile {worker.Key!.Value.ItemId}: weapon visibility re-applied {worker.Gate.Retries}x");
                worker.PoseGate = new PoseGate(PoseTimelines.For(worker.Key!.Value.Pose), worker.PoseChanged ? PoseBlendFrames : 0, PoseTimeoutFrames, worker.PoseChanged);
                worker.Phase = Phase.Pose;
                break;

            case Phase.Pose:
                var (playing, overridden) = view.BaseTimeline();
                var poseState = worker.StanceGate?.Step(playing) ?? worker.PoseGate!.Step(playing);
                if (poseState == PoseGateState.Wait)
                    return;
                if (poseDiagnosticsBudget > 0 && worker.Key!.Value.Pose != TileKey.StandardPose)
                {
                    poseDiagnosticsBudget--;
                    Plugin.Log.Information($"[pose] tile {worker.Key.Value.ItemId}: pose {worker.Key.Value.Pose}, wanted {PoseTimelines.For(worker.Key.Value.Pose)}, playing {playing}, override {overridden}, {worker.PoseGate?.Frames} frames, {poseState}");
                }
                if (poseState == PoseGateState.Timeout && !poseWarned)
                {
                    poseWarned = true;
                    Plugin.Log.Warning($"Idle pose {worker.Key!.Value.Pose} did not start playing; tiles may show the standard pose");
                }
                worker.Frames = 0;
                worker.Phase = Phase.Freeze;
                break;

            case Phase.Freeze:
                // Already frozen by the previous tile of this view: copy right away.
                if (!worker.Frozen)
                {
                    // Re-applied every frame: the game may still move the animation while the copy is prepared.
                    if (!view.FreezeAtEnd() && !freezeWarned)
                    {
                        freezeWarned = true;
                        Plugin.Log.Warning("Freezing the pose at its last frame failed; tiles show a running animation");
                    }
                    if (++worker.Frames < FreezeFrames)
                        return;
                    worker.Frozen = true;
                }
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
                if (WeaponDiagnosticsBudget > 0 && lookup(done.ItemId)?.Slot is EquipSlot.MainHand or EquipSlot.OffHand)
                {
                    WeaponDiagnosticsBudget--;
                    Plugin.Log.Information($"[diag] tile {done.ItemId}: {view.DescribeWeapons()}");
                }
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

    public void Dispose()
    {
        ReleaseAll();
        foreach (var view in suspended)
            view.ReleaseAfterConflict();
        suspended.Clear();
    }
}
