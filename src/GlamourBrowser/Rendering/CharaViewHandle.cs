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
        // Used by the game for EquipRestriction checks.
        view->Race = race;
        view->Sex = sex;
    }

    /// <summary>Call once per framework tick.</summary>
    public void Tick()
    {
        if (view != null)
            view->Render(FrameworkStruct.Instance()->FrameCounter);
    }

    public void SetItem(EquipSlot slot, uint itemId, byte stain0, byte stain1)
    {
        if (view != null && view->State == StateReady)
            view->SetItemSlotData((byte)slot, itemId, stain0, stain1, 0, false);
    }

    /// <summary>Dresses every slot except <paramref name="except"/>: from the snapshot, or empty for underwear.</summary>
    public void ApplyOutfit(OutfitSnapshot outfit, TileMode mode, EquipSlot except)
    {
        foreach (var slot in Enum.GetValues<EquipSlot>())
        {
            if (slot == except)
                continue;

            var entry = mode == TileMode.CurrentOutfit ? outfit.Find(slot) : null;
            SetItem(slot, entry?.ItemId ?? 0, entry?.Stain0 ?? 0, entry?.Stain1 ?? 0);
        }
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
    /// Forgets the view without releasing it: the game has taken over the slot, and Release
    /// would delete the game's object. Leaks one CharaView allocation.
    /// </summary>
    public void Abandon()
    {
        if (view == null)
            return;
        Plugin.Log.Warning($"CharaView on slot {Index} abandoned (slot taken by the game)");
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
