using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Numerics;
using System.Threading.Tasks;
using Dalamud.Interface.Textures;
using Dalamud.Interface.Textures.TextureWraps;
using GlamourBrowser.Core.CustomSets;
using GlamourBrowser.Core.Items;
using GlamourBrowser.Core.Rendering;
using GlamourBrowser.Core.Localization;

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
    // Upper bound for waiting until only one body animation carries weight (the blend has finished).
    private const int BlendTimeoutFrames = 90;
    // Drawing a weapon: wait at least this long if no timeline change is seen, until the timelines are stable, at most the timeout.
    private const int DrawMinFrames = 8;
    private const int DrawStableFrames = 6;
    private const int DrawTimeoutFrames = 240;
    // When the draw has not taken, sheathe and draw again every this many frames, at most MaxDrawRetries times. Right
    // after a weapon of another kind has loaded the game drops draws for a while (seen with /glamourdresser trace: a draw
    // seconds later takes), so the retries go on for a good part of the timeout.
    private const int DrawRetryFrames = 20;
    private const int MaxDrawRetries = 8;
    // Sheathing a weapon of another kind before the next weapon tile: at least this long, at most until the timeout. The
    // game drops sheathes as well (seen with /glamourdresser trace: the weapon stayed drawn and the next tile kept the old
    // stance), so one that has not taken is sent again every SheatheRetryFrames, at most MaxSheatheRetries times.
    internal const int SheatheMinFrames = 8;
    internal const int SheatheTimeoutFrames = 150;
    private const int SheatheRetryFrames = 20;
    private const int MaxSheatheRetries = 5;
    // The stance key when the look holds no main-hand weapon.
    internal const string OwnWeapon = "own";
    private static readonly TimeSpan IdleRelease = TimeSpan.FromSeconds(5);

    private enum Phase { WaitReady, Apply, Sheathe, Loading, Settle, Pose, Blend, Freeze, Copy }

    private sealed class Worker(CharaViewHandle view)
    {
        public CharaViewHandle View { get; } = view;
        public Phase Phase { get; set; } = Phase.WaitReady;
        public TileKey? Key { get; set; }
        /// <summary>The try-on around the tile's slots as it was when the key was taken: the key was made for it, so the tile is dressed in it.</summary>
        public IReadOnlyList<CustomPiece> Look { get; set; } = [];
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
        public DrawGate? DrawGate { get; set; }
        public int DrawRetries { get; set; }
        /// <summary>A retry toggled the weapon the other way last frame; this frame toggles it back.</summary>
        public bool RetryPending { get; set; }
        public TileSubject? Subject { get; set; }
        /// <summary>The tile shows the weapons (<see cref="LiveWeapons.TileShows"/>); else they are hidden, no default sword.</summary>
        public bool ShowWeapons { get; set; } = true;
        /// <summary>The kind of weapon in hand (<see cref="WeaponStance"/>), whose stance it is held in; null when sheathed.</summary>
        public string? InHandStance { get; set; }
        /// <summary>Diagnostics: frames spent sheathing before this tile (-1: none) and how drawing ended.</summary>
        public int SheatheFrames { get; set; } = -1;
        public int SheatheRetries { get; set; }
        public string? DrawOutcome { get; set; }
        /// <summary>What the view wears besides the tile's items: the look of this key (its try-on part) without these slots (a <see cref="SlotMask"/>).</summary>
        public (ulong Hash, ushort Except)? DressedAs { get; set; }
        public bool Busy => Key != null;
    }

    private readonly TileQueue queue;
    private readonly TileCache<IDalamudTextureWrap> cache;
    private readonly Func<TileKey, TileSubject?> lookup;
    private readonly AppearanceTracker appearance;
    private readonly Func<TileSubject?, CameraPreset?> cameraFor;
    // The try-on without the slots a tile covers and without what blocks them (TryOn.LookFor).
    private readonly Func<ushort, IReadOnlyList<CustomPiece>> look;
    private readonly Func<uint, GlamourItem?> itemOf;
    // The slots a tile leaves free of the try-on: its own, and the weapons while the live view hides them.
    private readonly Func<TileSubject, ushort> coverOf;
    private readonly List<Worker> workers = new();
    private readonly List<CharaViewHandle> suspended = new();
    private readonly Dictionary<TileKey, string> failures = new();
    private DateTime lastWork = DateTime.UtcNow;
    private bool liveReserved;
    private bool poseWarned;
    private bool freezeWarned;

    public RenderPool(TileQueue queue, TileCache<IDalamudTextureWrap> cache, Func<TileKey, TileSubject?> lookup,
                      AppearanceTracker appearance, Func<TileSubject?, CameraPreset?> cameraFor, Func<ushort, IReadOnlyList<CustomPiece>> look,
                      Func<uint, GlamourItem?> itemOf, Func<TileSubject, ushort> coverOf)
    {
        this.coverOf = coverOf;
        this.itemOf = itemOf;
        this.look = look;
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
            Status = Loc.T("Render-Plätze werden gerade vom Spiel genutzt (Gruppen-Portraits).");
            if (workers.Count > 0)
            {
                // Set aside, not released: the game may own these slots now.
                suspended.AddRange(workers.Select(w => w.View));
                workers.Clear();
                Disrupted?.Invoke();
            }
            return;
        }

        // Freed once the game gave their slots back.
        suspended.RemoveAll(view => view.TryReleaseAfterConflict());

        // Slots 0–4 only while no game window wants them; a view the game took over is never touched again.
        var sharedFree = !SlotGuard.WindowsActive();
        var lost = workers.Where(w => w.View.TakenOver || !sharedFree && RenderSlots.IsShared(w.View.Index)).ToList();
        if (lost.Count > 0)
        {
            foreach (var worker in lost)
                Drop(worker);
            Disrupted?.Invoke();
        }

        ManageWorkers(sharedFree);
        foreach (var worker in workers)
        {
            worker.View.Tick();
            Step(worker);
        }
    }

    private void ManageWorkers(bool sharedFree)
    {
        var wanted = RenderSlots.Select(MaxViews, liveReserved, sharedFree);

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
            Status = workers.Count == 0 && blocked > 0 ? Loc.T("Kein Render-Platz frei (Plätze 5–7 belegt).") : null;
        }
        else
        {
            Status = null;
            if (workers.Count > 0 && DateTime.UtcNow - lastWork > IdleRelease)
                ReleaseAll();
        }
    }

    /// <summary>The kind of weapon a tile draws: its own main-hand weapon, else the one its look holds in the main hand.</summary>
    private string StanceOf(Worker worker)
        => WeaponStance.Of(worker.Subject) ?? WeaponStance.OfLook(worker.Look, itemOf) ?? OwnWeapon;

    /// <summary>Dresses the view for the tile and starts loading it (Loading phase next).</summary>
    private void Dress(Worker worker, TileKey key, TileSubject subject)
    {
        var view = worker.View;
        // Re-dress when the slots change too, otherwise the previous tile's items (e.g. a hat) stay on the
        // character. Sets with the same slots as the previous tile only swap their pieces. A robe covers legs and
        // feet as well: the look around it leaves them out, so nothing put on afterwards takes the robe off again.
        var except = coverOf(subject);
        // The try-on is the look around the tile's items; the key holds its hash.
        var redressed = worker.DressedAs != (key.AppearanceHash, except);
        if (redressed)
        {
            view.ApplyOutfit(worker.Look, except);
            worker.DressedAs = (key.AppearanceHash, except);
        }
        // Items carry their dyes in the key; custom sets their own per slot, detected sets the key's tile dye.
        foreach (var piece in subject.Wear)
        {
            var (stain0, stain1) = subject is SetSubject ? TileDyes.Of(subject, piece, key.Dye) : (key.Stain0, key.Stain1);
            view.SetItem(piece.Slot, piece.Id, stain0, stain1);
        }
        // The weapon is drawn or sheathed once the item has loaded (Loading phase), and that has to finish
        // first (DrawGate in the Pose phase).
        view.ApplyCamera(cameraFor(subject));
        // A view keeps its pose and frozen moment from tile to tile; only a change (or a re-dress, which may
        // rebuild the character) lets the animation run again and waits for it in the Pose phase.
        // Weapon tiles always let the animation run: the weapon is attached to the hand by the animation.
        worker.PoseChanged = redressed || key.WeaponDrawn || worker.LastPose != key.Pose;
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
        worker.DrawGate = null;
        worker.RetryPending = false;
        worker.Frames = 0;
        worker.Phase = Phase.Loading;
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
                if (lookup(key) is not { } subject)
                {
                    failures[key] = Loc.T("Unbekanntes Item");
                    return;
                }

                worker.Key = key;
                worker.Subject = subject;
                // Taken now, not when dressing: sheathing first can take a while, and a click meanwhile changes the try-on
                // (the key would then hold another look than the tile shows, and could be shown again later).
                worker.Look = look(coverOf(subject));
                worker.ShowWeapons = LiveWeapons.TileShows(subject, worker.Look);
                worker.SheatheFrames = -1;
                worker.SheatheRetries = 0;
                worker.DrawOutcome = null;
                worker.Stopwatch.Restart();
                // The game picks the stance when a weapon is drawn and keeps it for the next weapon loaded into the hand:
                // a weapon of another kind is sheathed first, while nothing loads (a sheathe during a load is ignored),
                // and drawn anew once the new one has loaded (Loading phase).
                if (key.WeaponDrawn && view.IsWeaponDrawn() && worker.InHandStance != StanceOf(worker))
                {
                    view.SetAnimationPaused(false);
                    worker.Frozen = false;
                    view.SetWeaponDrawn(false);
                    worker.WeaponDrawn = false;
                    worker.InHandStance = null;
                    worker.Frames = 0;
                    worker.DrawRetries = 0;
                    worker.RetryPending = false;
                    worker.Phase = Phase.Sheathe;
                    return;
                }
                Dress(worker, key, subject);
                break;

            case Phase.Sheathe:
                worker.Frames++;
                if (worker.RetryPending)
                {
                    // Second half of a retry: sheathe again.
                    view.SetWeaponDrawn(false);
                    worker.RetryPending = false;
                    worker.DrawRetries++;
                    return;
                }
                if (view.IsWeaponDrawn() && !view.IsChangingStance() && worker.Frames % SheatheRetryFrames == 0 && worker.DrawRetries < MaxSheatheRetries)
                {
                    // The sheathe did not take, and the same toggle again does nothing: draw on this frame, sheathe on the next.
                    view.SetWeaponDrawn(true);
                    worker.RetryPending = true;
                    return;
                }
                // Sheathed to the end of its animation (no item loading), or given up after the timeout: the tile then keeps
                // the old stance.
                if (worker.Frames < SheatheMinFrames || (view.IsWeaponInHand() || view.IsChangingStance()) && worker.Frames < SheatheTimeoutFrames
                    || !view.IsIdle)
                    return;
                worker.SheatheFrames = worker.Frames;
                worker.SheatheRetries = worker.DrawRetries;
                Dress(worker, worker.Key!.Value, worker.Subject!);
                break;

            case Phase.Loading:
                worker.Frames++;
                // Give SetItemSlotData a frame to raise its flags, then wait until both are clear.
                if (worker.Frames < 2 || !view.IsIdle)
                {
                    if (worker.Frames > TimeoutFrames)
                        Fail(worker, Loc.T("Zeitüberschreitung beim Laden"));
                    return;
                }
                // Draw or sheathe now that the item has loaded (a toggle sent while an item loads is ignored). Judged by
                // the character itself, not only by what this worker did last time: the game keeps a drawn weapon drawn
                // across item loads, and a toggle can be dropped. The DrawGate waits for the stance to settle.
                // A toggle during a draw or sheathe animation is dropped: let that end first.
                if (view.IsChangingStance())
                {
                    if (worker.Frames > TimeoutFrames)
                        Fail(worker, Loc.T("Zeitüberschreitung beim Laden"));
                    return;
                }
                var wantDrawn = worker.Key!.Value.WeaponDrawn;
                var inHand = view.IsWeaponInHand();
                if (wantDrawn)
                    view.UseTryonJob();
                if (wantDrawn || worker.WeaponDrawn || inHand)
                {
                    if (inHand != wantDrawn || worker.WeaponDrawn != wantDrawn)
                        view.SetWeaponDrawn(wantDrawn);
                    if (!wantDrawn)
                    {
                        // Sheathing plays an animation: let it run, then settle and freeze anew.
                        view.SetAnimationPaused(false);
                        worker.Frozen = false;
                        worker.PoseChanged = true;
                    }
                    worker.WeaponDrawn = wantDrawn;
                    worker.InHandStance = wantDrawn ? StanceOf(worker) : null;
                    worker.DrawRetries = 0;
                    worker.RetryPending = false;
                    worker.DrawGate = new DrawGate(DrawMinFrames, DrawStableFrames, DrawTimeoutFrames);
                }
                view.ApplyVisibility(worker.ShowWeapons);
                worker.Gate = new SettleGate(SettleFrames, TimeoutFrames, MaxVisibilityRetries);
                worker.Phase = Phase.Settle;
                break;

            case Phase.Settle:
                // ApplyVisibility may start another short load, and a weapon that finished loading late
                // comes back hidden; the gate re-applies a bounded number of times.
                switch (worker.Gate!.Step(view.IsIdle, worker.ShowWeapons ? view.WeaponsShown() : view.WeaponsHidden()))
                {
                    case SettleAction.Wait:
                        return;
                    case SettleAction.Reapply:
                        view.ApplyVisibility(worker.ShowWeapons);
                        return;
                    case SettleAction.Timeout:
                        Fail(worker, Loc.T("Zeitüberschreitung beim Laden"));
                        return;
                    case SettleAction.CopyWithoutWeapons:
                        Plugin.Log.Warning($"Tile {worker.Key!.Value.ItemId}: weapons still {(worker.ShowWeapons ? "hidden" : "shown")} after {worker.Gate.Retries} retries, copying anyway");
                        break;
                }
                if (worker.Gate.Retries > 0)
                    Plugin.Log.Verbose($"Tile {worker.Key!.Value.ItemId}: weapon visibility re-applied {worker.Gate.Retries}x");
                worker.PoseGate = new PoseGate(PoseTimelines.For(worker.Key!.Value.Pose), worker.PoseChanged ? PoseBlendFrames : 0, PoseTimeoutFrames, worker.PoseChanged);
                worker.Phase = Phase.Pose;
                break;

            case Phase.Pose:
                var (playing, _) = view.BaseTimeline();
                var tileDrawn = worker.Key!.Value.WeaponDrawn;
                if (worker.RetryPending)
                {
                    // Second half of a retry: back to the wanted state.
                    view.SetWeaponDrawn(tileDrawn);
                    worker.RetryPending = false;
                    worker.DrawRetries++;
                }
                else if (worker.DrawGate != null && view.IsWeaponDrawn() != tileDrawn && !view.IsChangingStance()
                         && worker.DrawRetries < MaxDrawRetries && (worker.DrawGate.Frames + 1) % DrawRetryFrames == 0)
                {
                    // The toggle did not take (IsWeaponDrawn follows a toggle that took at once), and the same toggle
                    // again does nothing: toggle the other way on this frame, back on the next.
                    view.SetWeaponDrawn(!tileDrawn);
                    worker.RetryPending = true;
                }

                PoseGateState poseState;
                if (worker.DrawGate == null)
                    poseState = worker.PoseGate!.Step(playing);
                else
                {
                    poseState = worker.DrawGate.Step(view.TimelineSignature(), view.BodyBlend(), view.IsWeaponInHand() == tileDrawn);
                    // Sheathing brings the idle pose back as well: wait for both.
                    if (!tileDrawn)
                        poseState = PoseGateStates.Both(poseState, worker.PoseGate!.Step(playing));
                }
                if (poseState == PoseGateState.Wait)
                    return;
                if (worker.DrawGate != null)
                    worker.DrawOutcome = $"{poseState} after {worker.DrawGate.Frames} frames";
                if (poseState == PoseGateState.Timeout && !poseWarned)
                {
                    poseWarned = true;
                    Plugin.Log.Warning(worker.DrawGate == null ? $"Idle pose {worker.Key!.Value.Pose} did not start playing; tiles may show the standard pose"
                        : tileDrawn ? "Drawing the weapon did not settle in time; copying anyway"
                        : "Sheathing the weapon (or the idle pose) did not settle in time; copying anyway");
                }
                worker.Frames = 0;
                // After a change the blend from the previous animation has to finish before freezing.
                worker.Phase = worker.PoseChanged ? Phase.Blend : Phase.Freeze;
                break;

            case Phase.Blend:
                var activeAnimations = view.BodyBlend();
                worker.Frames++;
                if (activeAnimations > 1 && worker.Frames <= BlendTimeoutFrames)
                    return;
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
                    Fail(worker, Loc.T("Keine Textur"));
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
                    Fail(worker, task.Exception?.InnerException?.Message ?? Loc.T("Kopieren fehlgeschlagen"));
                    return;
                }

                var done = worker.Key!.Value;
                if (WeaponDiagnosticsBudget > 0 && lookup(done) is ItemSubject { Item: { Slot: EquipSlot.MainHand or EquipSlot.OffHand } weapon })
                {
                    WeaponDiagnosticsBudget--;
                    Plugin.Log.Information($"[diag] tile {done.ItemId} {weapon.Name}: stance {worker.InHandStance}, sheathe {worker.SheatheFrames}/{worker.SheatheRetries}, " +
                                           $"retries {worker.DrawRetries}, draw {worker.DrawOutcome} | {view.DescribeTrace()}");
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

    private void Release(Worker worker) => Drop(worker);

    /// <summary>Gives the worker's view up: released, or set aside while the game holds its slot.</summary>
    private void Drop(Worker worker)
    {
        if (worker.View.TakenOver)
            suspended.Add(worker.View);
        else
            worker.View.Dispose();
        workers.Remove(worker);
    }

    private void ReleaseAll()
    {
        var hadWork = workers.Any(w => w.Busy);
        foreach (var worker in workers.ToList())
            Drop(worker);
        if (hadWork)
            Disrupted?.Invoke();
    }

    public void Dispose()
    {
        ReleaseAll();
        // Still held by the game: left to it (a small leak instead of touching its character).
        foreach (var view in suspended)
            view.TryReleaseAfterConflict();
        suspended.Clear();
    }
}
