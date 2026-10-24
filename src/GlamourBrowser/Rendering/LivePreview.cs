using System;
using GlamourBrowser.Core.Items;
using GlamourBrowser.Core.Rendering;
using FFXIVClientStructs.FFXIV.Client.Graphics.Kernel;

namespace GlamourBrowser.Rendering;

/// <summary>Live, rotatable view of the selected item on slot 7, borrowed from the render pool.</summary>
public sealed unsafe class LivePreview : IDisposable
{
    private const uint Slot = 7;

    private readonly RenderPool pool;
    private readonly AppearanceTracker appearance;
    private readonly Configuration config;
    private readonly Func<EquipSlot, CameraPreset?> cameraFor;
    private CharaViewHandle? view;
    private CharaViewHandle? suspended;
    private (uint ItemId, TileMode Mode, ulong Hash, CameraPreset? Camera)? applied;
    private float rotateX, rotateY, panX, panY, zoom;
    private bool reset;

    public LivePreview(RenderPool pool, AppearanceTracker appearance, Configuration config, Func<EquipSlot, CameraPreset?> cameraFor)
    {
        this.pool = pool;
        this.appearance = appearance;
        this.config = config;
        this.cameraFor = cameraFor;
    }

    public GlamourItem? Selected { get; private set; }

    /// <summary>Weapon in hand instead of sheathed, toggled from the UI like the game's try-on window.</summary>
    public bool WeaponDrawn { get; set; }
    private bool? appliedWeaponDrawn;

    /// <summary>Weapon shown or hidden, toggled from the UI like the game's try-on window.</summary>
    public bool WeaponVisible { get; set; } = true;
    private bool? appliedWeaponVisible;

    public Texture* Texture => view != null ? view.GetTexture() : null;

    public void Select(GlamourItem? item) => Selected = item;

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

        if (Selected is not { } item)
        {
            ReleaseView();
            return;
        }

        pool.ReserveLiveSlot(true);
        if (view == null)
        {
            var character = AppearanceTracker.LocalCharacter();
            if (!pool.LiveSlotFree || character == null || !appearance.IsValid || !CharaViewHandle.IsSlotFree(Slot))
                return;
            view = new CharaViewHandle(Slot);
            view.Create(character, appearance.Race, appearance.Sex);
            applied = null;
            appliedWeaponDrawn = null;
            appliedWeaponVisible = null;
        }

        view.Tick();
        if (!view.IsIdle && view.State != CharaViewHandle.StateReady)
            return;

        var camera = cameraFor(item.Slot);
        var wanted = (item.Id, config.TileMode, appearance.Hash, camera);
        if (applied != wanted && view.IsIdle)
        {
            view.ApplyOutfit(appearance.Outfit, config.TileMode, item.Slot);
            view.SetItem(item.Slot, item.Id, 0, 0);
            view.ApplyCamera(camera);
            applied = wanted;
            appliedWeaponVisible = null;
        }

        // Visibility has to be applied after the new items have loaded.
        if (appliedWeaponVisible != WeaponVisible && view.IsIdle)
        {
            view.ApplyVisibility(WeaponVisible);
            appliedWeaponVisible = WeaponVisible;
        }

        if (appliedWeaponDrawn != WeaponDrawn && view.State == CharaViewHandle.StateReady)
        {
            view.SetWeaponDrawn(WeaponDrawn);
            appliedWeaponDrawn = WeaponDrawn;
            appliedWeaponVisible = null;
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
