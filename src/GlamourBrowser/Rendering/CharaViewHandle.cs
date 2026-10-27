using System;
using FFXIVClientStructs.FFXIV.Client.Game.Character;
using FFXIVClientStructs.FFXIV.Client.Game.Object;
using GameObjectStruct = FFXIVClientStructs.FFXIV.Client.Game.Object.GameObject;
using FFXIVClientStructs.FFXIV.Client.Graphics.Kernel;
using FFXIVClientStructs.FFXIV.Client.Graphics.Render;
using FFXIVClientStructs.FFXIV.Client.System.Memory;
using FFXIVClientStructs.FFXIV.Client.UI.Misc;
using System.Collections.Generic;
using GlamourBrowser.Core.CustomSets;
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
    /// Found by comparing both views with /glamourdresser diag.
    /// </summary>
    private const byte WeaponHiddenStateBit = 0x08;
    private const byte WeaponInHandBit = 0x04;
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
    public static bool IsSlotFree(uint index) => ObjectIn(index) == 0;

    private static nint ObjectIn(uint index) => (nint)ClientObjectManager.Instance()->GetObjectByIndex((ushort)(ClientObjectBase + index));

    // The character Initialize put into the slot: when another one lies there, the game took the slot over.
    private nint ownObject;

    /// <summary>
    /// The game put its own character into the slot (a game window opened on it) and deleted ours: the view must not be
    /// touched any more, only freed once the game is done (<see cref="TryReleaseAfterConflict"/>).
    /// </summary>
    public bool TakenOver => view != null && ownObject != 0 && ObjectIn(Index) != ownObject;

    public void Create(Character* source, byte race, byte sex)
    {
        if (view != null)
            return;

        view = IMemorySpace.GetUISpace()->Create<CharaView>();
        if (view == null)
            throw new InvalidOperationException("Could not allocate CharaView");

        view->Initialize(null, Index, 0);
        ownObject = ObjectIn(Index);
        // No character in the slot: the game had no client object left. Freed again without release (nothing of ours
        // is in the slot), so a takeover can never go unseen for want of an object to compare with.
        if (ownObject == 0)
        {
            Plugin.Log.Warning($"CharaView on slot {Index} got no character; not used");
            view->Dtor(0);
            IMemorySpace.Free(view);
            view = null;
            return;
        }
        view->ModelData.CopyFromCharacter(source);
        // CopyFromCharacter takes over the player's "hide weapon/headgear" settings; tiles must show both.
        view->ModelData.WeaponHidden = false;
        view->ModelData.HeadgearHidden = false;
        // Used by the game for EquipRestriction checks.
        view->Race = race;
        view->Sex = sex;
    }

    /// <summary>
    /// Gives the view's character no job, like the game's try-on window (seen with /glamourdresser diag): a weapon drawn
    /// afterwards is held in its own stance instead of the player's job's. Call before drawing.
    /// </summary>
    public void UseTryonJob()
    {
        var character = view != null && view->State == StateReady ? view->GetCharacter() : null;
        if (character != null)
            character->CharacterData.ClassJob = 0;
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

    /// <summary>Closes or opens the helmet's visor, like the game's "Manually Adjust Visor" (/visor); helmets without one stay as they are.</summary>
    public void SetVisor(bool closed)
    {
        var character = view != null && view->State == StateReady ? view->GetCharacter() : null;
        if (character != null && character->DrawData.IsVisorToggled != closed)
            character->DrawData.SetVisor(closed);
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

    /// <summary>Every loaded weapon is hidden (a tile without a main hand in its look).</summary>
    public bool WeaponsHidden()
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
            if (drawObject != null && (weapon.State & WeaponHiddenStateBit) == 0 && (drawObject->Flags & 0x01) != 0)
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
    /// Undresses the character completely (weapons included), then dresses every piece of the look except in
    /// <paramref name="exceptSlots"/> (a <see cref="SlotMask"/>); an empty look leaves the underwear.
    /// SetItemSlotData(slot, 0) does not clear a slot, so without UnequipGear items of earlier tiles (or the initial copy) would remain.
    /// </summary>
    public void ApplyOutfit(IReadOnlyList<CustomPiece> look, ushort exceptSlots)
    {
        if (view == null || view->State != StateReady)
            return;

        // Clears all 14 item entries and marks the view dirty (0x309), like the try-on window's
        // "hide other equipment".
        view->UnequipGear(false, true);
        foreach (var piece in look)
        {
            if (!SlotMask.Contains(exceptSlots, piece.Slot))
                SetItem(piece.Slot, piece.ItemId, piece.Stain0, piece.Stain1);
        }
    }

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
            var timeline = PoseTimelines.For(pose);
            character->Timeline.BaseOverride = timeline;
            // Poses 5 and 6 only have a loop of priority 7 (1 to 4 one of 4), which keeps playing once the override is gone
            // and covers the battle stance of a drawn weapon (seen with /glamourdresser trace): the standard idle ends it.
            ref var sequencer = ref character->Timeline.TimelineSequencer;
            if (timeline == 0 && PoseTimelines.IsPose(sequencer.TimelineIds[0]))
                sequencer.PlayTimeline(PoseTimelines.StandardIdle, null);
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

    public int BodyBlend() => view != null ? AnimationFreeze.BodyBlend(view->GetCharacter()) : 0;

    /// <summary>
    /// Whether the weapon is really in hand: DrawData.Flags2 bit 0x04 (set in the game's try-on window with a drawn
    /// weapon). Character.IsWeaponDrawn turns true earlier, while the weapon still hangs on the back.
    /// </summary>
    /// <summary>
    /// True while the character draws or sheathes its weapon: the timeline sequencer's second slot plays that animation
    /// (1 drawing, 2 sheathing; seen with /glamourdresser trace). The game drops a toggle sent meanwhile, and the in-hand bit
    /// clears halfway through sheathing, so this is what tells that the character is free again.
    /// </summary>
    public bool IsChangingStance()
    {
        var character = view != null ? view->GetCharacter() : null;
        return character != null && character->Timeline.TimelineSequencer.TimelineIds[1] != 0;
    }

    /// <summary>Whether the last draw or sheathe took: Character.IsWeaponDrawn follows it at once, unlike the in-hand bit.</summary>
    public bool IsWeaponDrawn()
    {
        var character = view != null ? view->GetCharacter() : null;
        return character != null && character->IsWeaponDrawn;
    }

    public bool IsWeaponInHand()
    {
        var character = view != null ? view->GetCharacter() : null;
        return character != null && (character->DrawData.Flags2 & WeaponInHandBit) != 0;
    }

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

    /// <summary>
    /// The view's absolute camera state. "Rotate" and "pan" move the character inside the view's
    /// scene (GameObject.Rotation / Position); "zoom" is a distance in the render camera.
    /// </summary>
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
               $"ModelData.WeaponHidden {view->ModelData.WeaponHidden} | " + DescribeCharacter(view->GetCharacter())
               + " || " + DescribeModelData(view);
    }

    /// <summary>Diagnostics for /glamourdresser trace: loading, weapon and animation state of a view in one short line.</summary>
    public static string DescribeTrace(CharaView* view)
    {
        if (view == null || view->State != StateReady)
            return "not ready";
        var character = view->GetCharacter();
        if (character == null)
            return "no character";
        ref var weapon = ref character->DrawData.WeaponData[0];
        return $"copied {view->CharacterDataCopied} loaded {view->CharacterLoaded} | drawn {character->IsWeaponDrawn} f2 {character->DrawData.Flags2:X2} " +
               $"w0 {weapon.ModelId.Value:X} st {weapon.State:X2} | {DescribeAnimation(character)}, blend {AnimationFreeze.BodyBlend(character)}";
    }

    public string DescribeTrace() => DescribeTrace(view);

    /// <summary>Diagnostics for automatic framing: camera, character and bones (see <see cref="CameraProbe"/>).</summary>
    public List<string> DescribeCamera() => CameraProbe.Describe(view, GetTexture());

    /// <summary>The bones automatic framing needs; null until the character is loaded.</summary>
    public BodyMeasures? MeasureBody() => view != null && view->State == StateReady ? CameraProbe.MeasureBody(view) : null;

    /// <summary>Diagnostics: the view's model data as hex (0x48–0xE0 of the CharaView), to compare views byte by byte.</summary>
    public static string DescribeModelData(CharaView* view)
    {
        if (view == null)
            return "no model data";
        var bytes = new ReadOnlySpan<byte>((byte*)view + 0x48, 0xE0 - 0x48);
        return $"modelData {Convert.ToHexString(bytes)}";
    }

    /// <summary>Diagnostics: job, animation state and the playing timelines of a character (the battle stance is one of them).</summary>
    public static string DescribeAnimation(Character* character)
    {
        if (character == null)
            return "no character";
        var timeline = &character->Timeline;
        var ids = new System.Text.StringBuilder();
        foreach (var id in timeline->TimelineSequencer.TimelineIds)
            ids.Append(id).Append(' ');
        return $"job {character->CharacterData.ClassJob}, mode {character->Mode}, modelState {timeline->ModelState}, " +
               $"baseOverride {timeline->BaseOverride}, timelines [{ids.ToString().TrimEnd()}]";
    }

    /// <summary>Diagnostics: weapon state, draw flags, scene links and positions of a character's body and weapons.</summary>
    public static string DescribeCharacter(Character* character)
    {
        if (character == null)
            return "no character";
        var text = $"{DescribeAnimation(character)}, IsWeaponHidden {character->DrawData.IsWeaponHidden}, IsWeaponDrawn {character->IsWeaponDrawn}, Flags1 {character->DrawData.Flags1:X2}, Flags2 {character->DrawData.Flags2:X2}";
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
    /// Cleans up a view that was set aside because the game may own its slot (party portraits, a game window). If our
    /// object is still in the slot, the game never took it and a normal release is right. If the slot is empty, the game
    /// used it and already deleted the object; then only our CharaView memory is freed (its camera objects leak, Release
    /// would touch the slot again). While the game's own character is still there, nothing happens: false, try later.
    /// </summary>
    public bool TryReleaseAfterConflict()
    {
        if (view == null)
            return true;

        var current = ObjectIn(Index);
        if (current != 0 && current == ownObject)
        {
            Dispose();
            return true;
        }
        if (current != 0)
            return false;

        Plugin.Log.Warning($"CharaView on slot {Index} was taken over by the game; freeing without release");
        view->Dtor(0);
        IMemorySpace.Free(view);
        view = null;
        return true;
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
