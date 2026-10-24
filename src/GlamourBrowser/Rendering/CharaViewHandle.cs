using System;
using FFXIVClientStructs.FFXIV.Client.Game.Character;
using FFXIVClientStructs.FFXIV.Client.Game.Object;
using GameObjectStruct = FFXIVClientStructs.FFXIV.Client.Game.Object.GameObject;
using FFXIVClientStructs.FFXIV.Client.Graphics.Kernel;
using FFXIVClientStructs.FFXIV.Client.Graphics.Render;
using FFXIVClientStructs.FFXIV.Client.System.Memory;
using FFXIVClientStructs.FFXIV.Client.UI.Misc;
using GlamourBrowser.Core.Items;
using GlamourBrowser.Core.Rendering;
using FrameworkStruct = FFXIVClientStructs.FFXIV.Client.System.Framework.Framework;

namespace GlamourBrowser.Rendering;

/// <summary>
/// Owns one CharaView of our own. All methods must run on the framework thread.
///
/// Findings from disassembling the game (build of 2026-10-21), verified by the spike:
///  - Initialize(agent, index, readyCallback) only stores agent/callback, sets State = 1 and
///    (re)creates a BattleChara in ClientObjectManager slot 240 + index, deleting what was there.
///    The agent is only passed to the optional callbacks, so null is fine when none are set.
///  - Render(frame) drives the state machine: 1 -> background, 5 -> camera, 2 -> load model
///    (from ModelData when no GetCharacter callback is set), 3 -> 6 (ready). In state 6 it applies
///    pending item changes and calls Update.
///  - SetItemSlotData sets CharacterDataCopied (0x309) = 1; Render applies it and sets
///    CharacterLoaded (0x30A) = 1 until the draw object finished loading, then clears both.
///  - The image ends up in RenderTargetManager.GetCharaViewTexture(index); there are 8 slots (0-7).
///  - Dtor(1) frees through the game's operator delete; we free through IMemorySpace instead.
/// </summary>
public sealed unsafe class CharaViewHandle : IDisposable
{
    public const uint StateReady = 6;

    /// <summary>
    /// Bit in DrawObjectData.State that keeps a weapon from being drawn in our views (its DrawObject
    /// then lacks Flags bit 0x01). The game's try-on view never has it set; clearing it shows the weapon.
    /// Found by comparing both views with /gbrowser diag.
    /// </summary>
    private const byte WeaponHiddenStateBit = 0x08;
    private const ushort ClientObjectBase = 240;

    private CharaView* view;

    public CharaViewHandle(uint index)
    {
        Index = index;
    }

    public uint Index { get; }
    public bool IsCreated => view != null;
    public uint State => view != null ? view->State : 0;

    /// <summary>True once the view is ready and no item change is loading.</summary>
    public bool IsIdle => view != null && view->State == StateReady && !view->CharacterDataCopied && !view->CharacterLoaded;

    /// <summary>A slot is free when no client object lives at 240 + index.</summary>
    public static bool IsSlotFree(uint index)
        => ClientObjectManager.Instance()->GetObjectByIndex((ushort)(ClientObjectBase + index)) == null;

    public void Create(Character* source, byte race, byte sex)
    {
        if (view != null)
            return;

        view = IMemorySpace.GetUISpace()->Create<CharaView>();
        if (view == null)
            throw new InvalidOperationException("Could not allocate CharaView");

        view->Initialize(null, Index, 0);
        view->ModelData.CopyFromCharacter(source);
        // CopyFromCharacter takes over the player's "hide weapon/headgear" settings; tiles must show both.
        view->ModelData.WeaponHidden = false;
        view->ModelData.HeadgearHidden = false;
        // Used by the game for EquipRestriction checks.
        view->Race = race;
        view->Sex = sex;
    }

    /// <summary>Call once per framework tick.</summary>
    public void Tick()
    {
        if (view == null)
            return;
        view->Render(FrameworkStruct.Instance()->FrameCounter);
    }

    public void SetItem(EquipSlot slot, uint itemId, byte stain0, byte stain1)
    {
        if (view != null && view->State == StateReady)
            view->SetItemSlotData((byte)slot, itemId, stain0, stain1, 0, false);
    }

    /// <summary>
    /// Shows headgear and shows/hides weapons on the view's character. HideHeadgear/HideWeapons mirror the
    /// game's try-on view (TryonCharaView vf10); weapons additionally need <see cref="WeaponHiddenStateBit"/>
    /// cleared. Call after the items have loaded.
    /// </summary>
    public void ApplyVisibility(bool showWeapons)
    {
        if (view == null || view->State != StateReady)
            return;
        var character = view->GetCharacter();
        if (character == null)
            return;
        character->DrawData.HideHeadgear(0, false);
        character->DrawData.HideWeapons(!showWeapons);
        for (var i = 0; i < 2; i++)
        {
            ref var weapon = ref character->DrawData.WeaponData[i];
            weapon.State = showWeapons
                ? (byte)(weapon.State & ~WeaponHiddenStateBit)
                : (byte)(weapon.State | WeaponHiddenStateBit);
        }
    }

    /// <summary>
    /// True when every weapon with a model has a draw object that is actually drawn: hidden bit clear and
    /// DrawObject.Flags bit 0x01 set. Weapons can finish loading after the view reports idle.
    /// </summary>
    public bool WeaponsShown()
    {
        var character = view != null && view->State == StateReady ? view->GetCharacter() : null;
        if (character == null)
            return true;

        for (var i = 0; i < 2; i++)
        {
            ref var weapon = ref character->DrawData.WeaponData[i];
            if (weapon.ModelId.Value == 0)
                continue;
            var drawObject = weapon.DrawData.DrawObject;
            if (drawObject == null || (weapon.State & WeaponHiddenStateBit) != 0 || (drawObject->Flags & 0x01) == 0)
                return false;
        }
        return true;
    }

    /// <summary>Draws the weapon (in hand) for weapon tiles, sheathes it otherwise.</summary>
    public void SetWeaponDrawn(bool drawn)
    {
        if (view != null && view->State == StateReady)
            view->ToggleDrawWeapon(drawn);
    }

    /// <summary>
    /// Undresses the character completely (weapons included), then dresses every slot except
    /// <paramref name="except"/> from the snapshot; underwear stays empty. SetItemSlotData(slot, 0) does not
    /// clear a slot, so without UnequipGear items of earlier tiles (or the initial copy) would remain.
    /// </summary>
    public void ApplyOutfit(OutfitSnapshot outfit, TileMode mode, EquipSlot except)
    {
        if (view == null || view->State != StateReady)
            return;

        // Clears all 14 item entries and marks the view dirty (0x309), like the try-on window's
        // "hide other equipment".
        view->UnequipGear(false, true);
        if (mode != TileMode.CurrentOutfit)
            return;

        foreach (var entry in outfit.Entries)
        {
            if (entry.Slot != except)
                SetItem(entry.Slot, entry.ItemId, entry.Stain0, entry.Stain1);
        }
    }

    /// <summary>
    /// The view's absolute camera state. "Rotate" and "pan" move the character inside the view's
    /// scene (GameObject.Rotation / Position); "zoom" is a distance in the render camera.
    /// </summary>
    /// <summary>
    /// Puts the character into an idle pose by overriding its base timeline (setting EmoteController.CPoseState
    /// alone does not change the animation, see the B2 pose spike). The standard pose clears the override.
    /// Returns false when there is no character or setting the override failed.
    /// </summary>
    public bool ApplyIdlePose(byte pose)
    {
        if (view == null)
            return false;
        try
        {
            var character = view->GetCharacter();
            if (character == null)
                return false;
            // A pose without its own timeline (e.g. the default idle) simply shows the standard pose.
            character->Timeline.BaseOverride = PoseTimelines.For(pose);
            return true;
        }
        catch (Exception e)
        {
            Plugin.Log.Warning(e, $"ApplyIdlePose {pose} failed");
            return false;
        }
    }

    /// <summary>Freezes the character's animations at their last frame, see <see cref="AnimationFreeze"/>.</summary>
    public bool FreezeAtEnd() => view != null && AnimationFreeze.FreezeAtEnd(view->GetCharacter());

    public bool SetAnimationPaused(bool paused) => view != null && AnimationFreeze.SetPaused(view->GetCharacter(), paused);

    public (int Active, string Weights) BodyBlend() => view != null ? AnimationFreeze.BodyBlend(view->GetCharacter()) : (0, "");

    /// <summary>A hash over all timeline slots of the character, to see when its animations stop changing.</summary>
    public int TimelineSignature()
    {
        var character = view != null ? view->GetCharacter() : null;
        if (character == null)
            return 0;
        var hash = new HashCode();
        foreach (var id in character->Timeline.TimelineSequencer.TimelineIds)
            hash.Add(id);
        return hash.ToHashCode();
    }

    /// <summary>The base (slot 0) timeline the character is playing right now, and its base override.</summary>
    public (ushort Playing, ushort Override) BaseTimeline()
    {
        if (view == null)
            return (0, 0);
        var character = view->GetCharacter();
        if (character == null)
            return (0, 0);
        return (character->Timeline.TimelineSequencer.TimelineIds[0], character->Timeline.BaseOverride);
    }

    public CameraPreset? ReadCamera()
    {
        var character = ViewCharacter();
        var distance = DistanceField();
        if (character == null || distance == null)
            return null;
        return new CameraPreset(character->Rotation, character->Position.X, character->Position.Y, *distance);
    }

    /// <summary>Resets the camera to the game's default view, then applies the calibration if any.</summary>
    public void ApplyCamera(CameraPreset? preset)
    {
        if (view == null || view->State != StateReady)
            return;

        view->ResetPositions();
        if (preset is not { } p)
            return;

        var character = ViewCharacter();
        var distance = DistanceField();
        if (character == null || distance == null)
            return;
        character->SetRotation(p.Rotation);
        character->SetPosition(p.PositionX, p.PositionY, character->Position.Z);
        *distance = p.Distance;
    }

    /// <summary>Diagnostics: weapon visibility state of this view's character.</summary>
    public string DescribeWeapons()
    {
        if (view == null)
            return $"slot {Index}: no view";
        return $"slot {Index}: state {view->State}, copied {view->CharacterDataCopied}, loaded {view->CharacterLoaded}, idle {IsIdle}, " +
               $"ModelData.WeaponHidden {view->ModelData.WeaponHidden} | " + DescribeCharacter(view->GetCharacter());
    }

    /// <summary>Diagnostics: weapon state, draw flags, scene links and positions of a character's body and weapons.</summary>
    public static string DescribeCharacter(Character* character)
    {
        if (character == null)
            return "no character";
        var text = $"IsWeaponHidden {character->DrawData.IsWeaponHidden}, IsWeaponDrawn {character->IsWeaponDrawn}, Flags1 {character->DrawData.Flags1:X2}, Flags2 {character->DrawData.Flags2:X2}";
        var body = character->GameObject.DrawObject;
        if (body == null)
            return text + ", no body";
        text += $", body {(nint)body:X} flags {body->Flags:X2} objFlags {body->Object.ObjectFlags:X} pos {body->Object.Position} scale {body->Object.Scale} child {(nint)body->Object.ChildObject:X}";
        for (var i = 0; i < 2; i++)
        {
            ref var w = ref character->DrawData.WeaponData[i];
            text += $" || weapon{i}: model {w.ModelId.Value:X}, State {w.State:X2}, Flags1 {w.Flags1:X4}, Flags2 {w.Flags2:X2}";
            var o = w.DrawData.DrawObject;
            if (o != null)
                text += $", obj {(nint)o:X} flags {o->Flags:X2} objFlags {o->Object.ObjectFlags:X} parent {(nint)o->Object.ParentObject:X} pos {o->Object.Position} scale {o->Object.Scale}";
        }
        return text;
    }

    private GameObjectStruct* ViewCharacter()
        => view != null && view->State == StateReady ? (GameObjectStruct*)view->GetCharacter() : null;

    // SetCameraDistance (vf4) clamps and writes *(Camera + 0xE0) + 0x1FC.
    private float* DistanceField()
    {
        if (view == null || view->Camera == null)
            return null;
        var renderCamera = *(byte**)((byte*)view->Camera + 0xE0);
        return renderCamera == null ? null : (float*)(renderCamera + 0x1FC);
    }

    public void Rotate(float yaw, float pitch)
    {
        if (view != null && view->State == StateReady)
            view->SetCameraYawAndPitch(yaw, pitch);
    }

    public void Pan(float x, float y)
    {
        if (view != null && view->State == StateReady)
            view->SetCameraXAndY(x, y);
    }

    public void Zoom(float delta)
    {
        if (view != null && view->State == StateReady)
            view->SetCameraDistance(delta);
    }

    public void ResetCamera()
    {
        if (view != null && view->State == StateReady)
            view->ResetPositions();
    }

    public Texture* GetTexture()
    {
        if (view == null || view->State != StateReady)
            return null;
        var rtm = RenderTargetManager.Instance();
        return rtm == null ? null : rtm->GetCharaViewTexture(Index);
    }

    /// <summary>
    /// Cleans up a view that was set aside while the game's party portraits were active. If our object is
    /// still in the slot, the game never took it and a normal release is right. If the slot is empty, the
    /// game used it and already deleted the object; then only our CharaView memory is freed (its camera
    /// objects leak, Release would touch the slot again).
    /// </summary>
    public void ReleaseAfterConflict()
    {
        if (view == null)
            return;

        if (!IsSlotFree(Index))
        {
            Dispose();
            return;
        }

        Plugin.Log.Warning($"CharaView on slot {Index} was taken over by the game; freeing without release");
        view->Dtor(0);
        IMemorySpace.Free(view);
        view = null;
    }

    public void Dispose()
    {
        if (view == null)
            return;

        view->Release();
        view->Dtor(0);
        IMemorySpace.Free(view);
        view = null;
    }
}
