using System;
using FFXIVClientStructs.FFXIV.Client.Game.Character;
using FFXIVClientStructs.FFXIV.Client.Graphics.Kernel;
using FFXIVClientStructs.FFXIV.Client.Graphics.Render;
using FFXIVClientStructs.FFXIV.Client.System.Memory;
using FFXIVClientStructs.FFXIV.Client.UI.Misc;
using FrameworkStruct = FFXIVClientStructs.FFXIV.Client.System.Framework.Framework;

namespace GlamourSpike;

/// <summary>
/// Owns one CharaView of our own. All methods must run on the framework thread.
///
/// Findings from disassembling the game (patch of 2026-10-21):
///  - Initialize(agent, index, readyCallback) only stores agent/callback, sets State = 1 and
///    (re)creates a BattleChara in ClientObjectManager slot 240 + index. The agent is only passed
///    to the optional callbacks, so null is fine when no callbacks are set.
///  - Render(frame) drives the state machine: 1 -> background, 5 -> camera, 2 -> load model
///    (from ModelData when no GetCharacter callback is set), 3 -> 6 (ready). In state 6 it applies
///    pending item changes and calls Update.
///  - SetItemSlotData sets CharacterDataCopied (0x309) = 1; Render applies it and sets
///    CharacterLoaded (0x30A) = 1 until the draw object finished loading, then clears both.
///  - The image ends up in RenderTargetManager.GetCharaViewTexture(index); there are 8 slots (0-7).
/// </summary>
public sealed unsafe class SpikeView : IDisposable
{
    public const int StateReady = 6;

    private CharaView* view;

    public uint Index { get; }
    public bool IsCreated => view != null;
    public uint State => view != null ? view->State : 0;

    /// <summary>True once the view is ready and no item change is loading.</summary>
    public bool IsIdle => view != null && view->State == StateReady && !view->CharacterDataCopied && !view->CharacterLoaded;

    public SpikeView(uint index)
    {
        Index = index;
    }

    public void Create(Character* source)
    {
        if (view != null)
            return;

        view = IMemorySpace.GetUISpace()->Create<CharaView>();
        if (view == null)
            throw new InvalidOperationException("Could not allocate CharaView");

        view->Initialize(null, Index, 0);
        CopyFrom(source);
        Plugin.Log.Information($"CharaView created at {(nint)view:X} index {Index} clientObjectId {view->ClientObjectId}");
    }

    public void CopyFrom(Character* source)
    {
        if (view == null || source == null)
            return;

        view->ModelData.CopyFromCharacter(source);
        // Used by the game for EquipRestriction checks.
        view->Race = source->DrawData.CustomizeData.Race;
        view->Sex = source->DrawData.CustomizeData.Sex;
    }

    /// <summary>Call once per framework tick.</summary>
    public void Tick()
    {
        if (view == null)
            return;
        view->Render(FrameworkStruct.Instance()->FrameCounter);
    }

    /// <summary>Slot ids follow the EquippedItems container: 0 main, 1 off, 2 head, 3 body, 4 hands, 6 legs, 7 feet.</summary>
    public void SetItem(byte slot, uint itemId, byte stain0 = 0, byte stain1 = 0)
    {
        if (view == null || view->State != StateReady)
            return;
        view->SetItemSlotData(slot, itemId, stain0, stain1, 0, false);
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

    public void Dispose()
    {
        if (view == null)
            return;

        Plugin.Log.Information($"Releasing CharaView index {Index}");
        view->Release();
        // Dtor(1) would free through the game's operator delete; free through the same
        // memory space we allocated from instead.
        view->Dtor(0);
        IMemorySpace.Free(view);
        view = null;
    }
}
