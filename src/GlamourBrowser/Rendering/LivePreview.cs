using System;
using System.Collections.Generic;
using GlamourBrowser.Core.CustomSets;
using GlamourBrowser.Core.Items;
using GlamourBrowser.Core.Rendering;
using FFXIVClientStructs.FFXIV.Client.Graphics.Kernel;

namespace GlamourBrowser.Rendering;

/// <summary>
/// Live, rotatable view of the try-on (or the set editor's draft) on slot 7, borrowed from the render pool; the selected
/// item frames it (camera slot) and gets the weapon toggles.
/// </summary>
public sealed unsafe class LivePreview : IDisposable
{
    private const uint Slot = 7;
    // A toggle that has not taken after this many ticks (with the character free) was dropped and is sent anew, at most
    // MaxToggleRetries times in a row. Right after a weapon of another kind has loaded the game drops draws for a while
    // (seen with /gbrowser trace: a draw seconds later takes), so this keeps trying for a few seconds.
    private const int ToggleCheckTicks = 12;
    private const int MaxToggleRetries = 15;

    private readonly RenderPool pool;
    private readonly AppearanceTracker appearance;
    private readonly Configuration config;
    private readonly Func<EquipSlot, CameraPreset?> cameraFor;
    private CharaViewHandle? view;
    private CharaViewHandle? suspended;
    // The look's hash with face and body: any change of the try-on, the draft or the character dresses again.
    private (uint Look, ulong Customize, CameraPreset? Camera)? applied;
    private byte? appliedPose;
    private float rotateX, rotateY, panX, panY, zoom;
    private bool reset;

    public LivePreview(RenderPool pool, AppearanceTracker appearance, Configuration config, Func<EquipSlot, CameraPreset?> cameraFor)
    {
        this.pool = pool;
        this.appearance = appearance;
        this.config = config;
        this.cameraFor = cameraFor;
    }

    public TileSubject? Selected { get; private set; }

    /// <summary>What the character wears: the try-on, or the set editor's draft over the try-on's basis (set in Plugin).</summary>
    public Func<IReadOnlyList<CustomPiece>> Look { get; set; } = () => [];

    /// <summary>Weapon in hand instead of sheathed, toggled from the UI like the game's try-on window.</summary>
    public bool WeaponDrawn { get; set; }
    private bool? appliedWeaponDrawn;
    // The kind of weapon in hand (WeaponStance, RenderPool.OwnWeapon for the outfit's own); null when sheathed.
    private string? inHandStance;
    // Frames spent sheathing before another kind of weapon loads; -1 while not sheathing.
    private int sheatheFrames = -1;
    private int toggleTicks;
    private int toggleRetries;

    /// <summary>Weapon shown or hidden, toggled from the UI like the game's try-on window.</summary>
    public bool WeaponVisible { get; set; } = true;
    private bool? appliedWeaponVisible;

    public Texture* Texture => view != null ? view.GetTexture() : null;

    public void Select(TileSubject? subject) => Selected = subject;

    public void QueueRotate(float dx, float dy)
    {
        rotateX += dx;
        rotateY += dy;
    }

    public void QueuePan(float dx, float dy)
    {
        panX += dx;
        panY += dy;
    }

    public void QueueZoom(float wheel) => zoom += wheel;

    public void QueueReset() => reset = true;

    /// <summary>The live view's current camera state, for saving as the selected slot's calibration.</summary>
    public CameraPreset? CaptureCamera() => view?.ReadCamera();

    public string DescribeWeapons() => view?.DescribeWeapons() ?? "live: no view";

    /// <summary>Diagnostics for /gbrowser trace: what the preview asked for and the view's state.</summary>
    public string DescribeTrace()
        => $"want drawn {WeaponDrawn} applied {appliedWeaponDrawn} sheathe {sheatheFrames} stance {inHandStance} | {view?.DescribeTrace() ?? "no view"}";

    /// <summary>Call once per framework tick, after the render pool.</summary>
    public void Tick()
    {
        if (SlotGuard.ConflictActive())
        {
            // Set aside, not released: the game may own the slot now.
            if (view != null)
            {
                suspended = view;
                view = null;
                applied = null;
            }
            return;
        }

        if (suspended != null)
        {
            suspended.ReleaseAfterConflict();
            suspended = null;
        }

        // Nothing selected: the look with the full-body camera, never an empty view.
        var subject = Selected;
        pool.ReserveLiveSlot(true);
        if (view == null)
        {
            var character = AppearanceTracker.LocalCharacter();
            if (!pool.LiveSlotFree || character == null || !appearance.IsValid || !CharaViewHandle.IsSlotFree(Slot))
                return;
            view = new CharaViewHandle(Slot);
            view.Create(character, appearance.Race, appearance.Sex);
            applied = null;
            appliedPose = null;
            appliedWeaponDrawn = null;
            appliedWeaponVisible = null;
            inHandStance = null;
            sheatheFrames = -1;
        }

        view.Tick();
        if (!view.IsIdle && view.State != CharaViewHandle.StateReady)
            return;

        // The selection only frames the look: its slot's camera, the body's without one (like sets).
        var look = Look();
        var camera = cameraFor(subject?.CameraSlot ?? EquipSlot.Body);
        var wanted = (Looks.Hash(look), appearance.CustomizeHash, camera);

        // The game picks the stance when a weapon is drawn and keeps it for the next weapon loaded into the hand: before
        // another kind of weapon loads, the drawn one is sheathed (only while nothing loads), and drawn anew below.
        var (weaponDrawn, weaponVisible) = LiveWeapons.Effective(subject, WeaponDrawn, WeaponVisible);
        var stance = WeaponStance.Of(subject) ?? RenderPool.OwnWeapon;
        if (applied != wanted && weaponDrawn && appliedWeaponDrawn == true && inHandStance != stance && sheatheFrames < 0 && view.IsIdle
            && !view.IsChangingStance())
        {
            view.SetWeaponDrawn(false);
            appliedWeaponDrawn = false;
            inHandStance = null;
            sheatheFrames = 0;
        }
        if (sheatheFrames >= 0)
        {
            // Sheathed to the end of its animation, or given up after the timeout (the weapon then keeps the old stance).
            if (++sheatheFrames < RenderPool.SheatheMinFrames
                || (view.IsWeaponInHand() || view.IsChangingStance()) && sheatheFrames < RenderPool.SheatheTimeoutFrames || !view.IsIdle)
            {
                ApplyCameraInput(view);
                return;
            }
            sheatheFrames = -1;
        }

        if (applied != wanted && view.IsIdle)
        {
            view.ApplyOutfit(look, 0);
            view.ApplyCamera(camera);
            applied = wanted;
            appliedPose = null;
            appliedWeaponVisible = null;
        }

        // With the weapon drawn the battle stance is the base animation: an idle pose override would hide it.
        var pose = config.UseIdlePose && !weaponDrawn ? appearance.IdlePose : TileKey.StandardPose;
        if (appliedPose != pose && view.State == CharaViewHandle.StateReady)
        {
            view.ApplyIdlePose(pose);
            appliedPose = pose;
        }

        // Visibility has to be applied after the new items have loaded.
        if (appliedWeaponVisible != weaponVisible && view.IsIdle)
        {
            view.ApplyVisibility(weaponVisible);
            appliedWeaponVisible = weaponVisible;
        }

        // Drawn or sheathed once the items have loaded and no draw or sheathe is playing (the game drops a toggle meanwhile);
        // drawn without a job like the game's try-on window, so the weapon is held in its own stance.
        if (appliedWeaponDrawn != weaponDrawn && view.IsIdle && !view.IsChangingStance())
        {
            if (weaponDrawn)
                view.UseTryonJob();
            view.SetWeaponDrawn(weaponDrawn);
            appliedWeaponDrawn = weaponDrawn;
            inHandStance = weaponDrawn ? stance : null;
            appliedWeaponVisible = null;
            toggleTicks = 0;
        }
        else if (appliedWeaponDrawn is { } sent && view.IsWeaponDrawn() != sent && view.IsIdle && !view.IsChangingStance())
        {
            // Not taken: the same toggle again does nothing, so toggle the other way now and back on the next tick (above).
            if (++toggleTicks >= ToggleCheckTicks && toggleRetries < MaxToggleRetries)
            {
                view.SetWeaponDrawn(!sent);
                appliedWeaponDrawn = !sent;
                toggleRetries++;
                toggleTicks = 0;
            }
        }
        else if (appliedWeaponDrawn == view.IsWeaponDrawn())
        {
            toggleTicks = 0;
            toggleRetries = 0;
        }

        ApplyCameraInput(view);
    }

    private void ApplyCameraInput(CharaViewHandle target)
    {
        if (rotateX != 0 || rotateY != 0)
            target.Rotate(rotateX * config.RotateSensitivity, rotateY * config.RotateSensitivity);
        // Vertical pan is inverted so the character follows the mouse.
        if (panX != 0 || panY != 0)
            target.Pan(panX * config.PanSensitivity, -panY * config.PanSensitivity);
        if (zoom != 0)
            target.Zoom(-zoom * config.ZoomSensitivity);
        if (reset)
            target.ApplyCamera(applied?.Camera);

        rotateX = rotateY = panX = panY = zoom = 0;
        reset = false;
    }

    /// <summary>Gives the view back (window closed, logged out); the next tick after a new selection creates it again.</summary>
    public void Release()
    {
        Selected = null;
        ReleaseView();
    }

    private void ReleaseView()
    {
        if (view != null)
        {
            view.Dispose();
            view = null;
            applied = null;
        }
        pool.ReserveLiveSlot(false);
    }

    public void Dispose()
    {
        ReleaseView();
        suspended?.ReleaseAfterConflict();
        suspended = null;
    }
}
