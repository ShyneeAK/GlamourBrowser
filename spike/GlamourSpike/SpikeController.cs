using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Numerics;
using System.Threading.Tasks;
using Dalamud.Interface.Textures;
using Dalamud.Interface.Textures.TextureWraps;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.Game.Character;
using Lumina.Excel.Sheets;

namespace GlamourSpike;

public sealed class Tile
{
    public required uint ItemId { get; init; }
    public required string Name { get; init; }
    public IDalamudTextureWrap? Texture { get; set; }
    public double Milliseconds { get; set; }
    public int LoadFrames { get; set; }
    public uint ViewIndex { get; set; }
    public string? Error { get; set; }
}

/// <summary>Drives the CharaViews and the tile rendering. Everything runs on the framework thread.</summary>
public sealed unsafe class SpikeController : IDisposable
{
    private const byte BodySlot = 3;

    private enum Phase { WaitReady, Apply, Loading, Settle, Copy }

    /// <summary>One CharaView rendering tiles one after another.</summary>
    private sealed class Worker
    {
        public required SpikeView View { get; init; }
        public bool OwnsView { get; init; }
        public Phase Phase { get; set; } = Phase.WaitReady;
        public Tile? Tile { get; set; }
        public int Frames { get; set; }
        public int LoadFrames { get; set; }
        public Stopwatch Stopwatch { get; } = new();
        public Task<IDalamudTextureWrap>? CopyTask { get; set; }
    }

    private SpikeView? view;
    private readonly List<Worker> workers = new();
    private int nextTile;
    private int finishedTiles;
    private bool helpersSnapshotTaken;
    private readonly Stopwatch totalStopwatch = new();
    private List<Item> bodyItems = new();

    public uint Index { get; set; } = 7;
    public int ParallelViews { get; set; } = 3;
    public int SettleFrames { get; set; } = 1;
    public int TileWidth { get; set; } = 256;
    public List<Tile> Tiles { get; } = new();
    public uint? LiveItemId { get; private set; }
    public string Status { get; private set; } = "Nicht gestartet";
    public double? TotalMilliseconds { get; private set; }
    public MemoryProbe Memory { get; } = new();

    public bool IsCreated => view?.IsCreated == true;
    public bool IsGridRunning => workers.Count > 0;
    public uint ViewState => view?.State ?? 0;
    public bool ViewIdle => view?.IsIdle == true;
    public SpikeView? View => view;

    public SpikeController()
    {
        // Body pieces without gender/race locks.
        bodyItems = Plugin.DataManager.GetExcelSheet<Item>()
            .Where(i => i.EquipSlotCategory.RowId != 0 && i.EquipSlotCategory.Value.Body == 1
                        && i.ModelMain != 0 && i.EquipRestriction.RowId <= 1 && !i.Name.IsEmpty)
            .OrderBy(i => i.RowId)
            .ToList();
        PickItems(12);
    }

    /// <summary>Spread of <paramref name="count"/> body pieces over the whole item list.</summary>
    public void PickItems(int count)
    {
        if (IsGridRunning)
            return;

        DisposeTiles();
        Tiles.Clear();
        if (bodyItems.Count == 0)
            return;

        var step = Math.Max(1, bodyItems.Count / count);
        for (var n = 0; n < count && n * step < bodyItems.Count; n++)
        {
            var item = bodyItems[n * step];
            Tiles.Add(new Tile { ItemId = item.RowId, Name = item.Name.ExtractText() });
        }

        Plugin.Log.Information($"Picked {Tiles.Count} body items out of {bodyItems.Count}");
    }

    public void Start()
    {
        if (view != null)
            return;

        var player = Plugin.ObjectTable.LocalPlayer;
        if (player == null)
        {
            Status = "Kein Charakter eingeloggt";
            return;
        }

        Memory.Clear();
        Memory.Snapshot("Vor Start");

        try
        {
            view = new SpikeView(Index);
            view.Create((Character*)player.Address);
            Status = "CharaView angelegt, warte auf Bereit-Status";
        }
        catch (Exception e)
        {
            Plugin.Log.Error(e, "Creating CharaView failed");
            Status = $"Fehler: {e.Message}";
            view?.Dispose();
            view = null;
        }
    }

    public void Stop()
    {
        CancelGrid();
        view?.Dispose();
        view = null;
        LiveItemId = null;
        Status = "Gestoppt";
    }

    // Camera input from the UI, applied on the next framework tick.
    private float pendingYaw, pendingPitch, pendingZoom, pendingPanX, pendingPanY;
    private bool pendingReset;

    public void QueueRotate(float yaw, float pitch)
    {
        pendingYaw += yaw;
        pendingPitch += pitch;
    }

    public void QueuePan(float x, float y)
    {
        pendingPanX += x;
        pendingPanY += y;
    }

    public void QueueZoom(float delta) => pendingZoom += delta;

    public void QueueCameraReset() => pendingReset = true;

    public void ShowLive(uint itemId)
    {
        if (view == null || IsGridRunning)
            return;
        view.SetItem(BodySlot, itemId);
        LiveItemId = itemId;
    }

    public void ResetToOwnGear()
    {
        if (view == null || IsGridRunning)
            return;
        ApplyOwnBody(view);
        LiveItemId = null;
    }

    private static void ApplyOwnBody(SpikeView target)
    {
        var container = InventoryManager.Instance()->GetInventoryContainer(InventoryType.EquippedItems);
        var slot = container != null ? container->GetInventorySlot(BodySlot) : null;
        if (slot == null)
            return;

        var itemId = slot->GlamourId != 0 ? slot->GlamourId : slot->ItemId;
        target.SetItem(BodySlot, itemId, slot->Stains[0], slot->Stains[1]);
    }

    public void StartGrid()
    {
        if (view == null || IsGridRunning || Tiles.Count == 0)
            return;

        var player = Plugin.ObjectTable.LocalPlayer;
        if (player == null)
            return;

        DisposeTiles();
        foreach (var tile in Tiles)
        {
            tile.Error = null;
            tile.Milliseconds = 0;
            tile.LoadFrames = 0;
        }

        // The live view renders tiles too; helpers take the next lower slots and are released afterwards.
        workers.Add(new Worker { View = view, OwnsView = false });
        for (var n = 1; n < ParallelViews && Index >= n; n++)
        {
            var helper = new SpikeView(Index - (uint)n);
            helper.Create((Character*)player.Address);
            workers.Add(new Worker { View = helper, OwnsView = true });
        }

        nextTile = 0;
        finishedTiles = 0;
        helpersSnapshotTaken = workers.Count == 1;
        TotalMilliseconds = null;
        totalStopwatch.Restart();
        Plugin.Log.Information($"Grid render started with {workers.Count} views, settle {SettleFrames} frames");
    }

    private void CancelGrid()
    {
        foreach (var worker in workers.Where(w => w.OwnsView))
            worker.View.Dispose();
        workers.Clear();
    }

    public void ReleaseTiles()
    {
        if (IsGridRunning)
            return;
        DisposeTiles();
        Memory.Snapshot("Kacheln freigegeben");
    }

    public void OnFrameworkUpdate(IFramework framework)
    {
        if (view == null)
            return;

        try
        {
            if (pendingYaw != 0 || pendingPitch != 0)
                view.Rotate(pendingYaw, pendingPitch);
            if (pendingPanX != 0 || pendingPanY != 0)
                view.Pan(pendingPanX, pendingPanY);
            if (pendingZoom != 0)
                view.Zoom(pendingZoom);
            if (pendingReset)
                view.ResetCamera();
            pendingYaw = pendingPitch = pendingZoom = pendingPanX = pendingPanY = 0;
            pendingReset = false;

            view.Tick();
            foreach (var worker in workers.Where(w => w.OwnsView))
                worker.View.Tick();

            if (view.State == SpikeView.StateReady && Status.StartsWith("CharaView angelegt"))
            {
                Status = "Bereit";
                LogTextureInfo();
                Memory.Snapshot("Live-Ansicht bereit");
            }

            if (IsGridRunning)
                StepGrid();
        }
        catch (Exception e)
        {
            Plugin.Log.Error(e, "Framework tick failed, stopping");
            Stop();
            Status = $"Fehler im Tick: {e.Message}";
        }
    }

    private void StepGrid()
    {
        if (!helpersSnapshotTaken && workers.All(w => w.View.State == SpikeView.StateReady))
        {
            helpersSnapshotTaken = true;
            Memory.Snapshot($"{workers.Count} Ansichten bereit");
        }

        foreach (var worker in workers)
            StepWorker(worker);

        if (finishedTiles < Tiles.Count)
            return;

        TotalMilliseconds = totalStopwatch.Elapsed.TotalMilliseconds;
        Plugin.Log.Information($"Grid render finished: {Tiles.Count} tiles in {TotalMilliseconds:F0} ms with {workers.Count} views");
        Memory.Snapshot($"Raster fertig ({workers.Count} Ansichten)");
        CancelGrid();
        Memory.Snapshot("Helfer freigegeben");

        if (LiveItemId is { } live)
            view!.SetItem(BodySlot, live);
        else
            ApplyOwnBody(view!);
    }

    private void StepWorker(Worker worker)
    {
        var target = worker.View;
        switch (worker.Phase)
        {
            case Phase.WaitReady:
                if (target.IsIdle)
                    worker.Phase = Phase.Apply;
                break;

            case Phase.Apply:
                if (nextTile >= Tiles.Count || !target.IsIdle)
                    return;
                worker.Tile = Tiles[nextTile++];
                worker.Tile.ViewIndex = target.Index;
                worker.Stopwatch.Restart();
                target.SetItem(BodySlot, worker.Tile.ItemId);
                worker.Frames = 0;
                worker.Phase = Phase.Loading;
                break;

            case Phase.Loading:
                worker.Frames++;
                // Give SetItemSlotData a frame to raise its flags, then wait until both are clear.
                if (worker.Frames < 2 || !target.IsIdle)
                {
                    if (worker.Frames > 600)
                        FinishTile(worker, "Timeout beim Laden (600 Frames)");
                    return;
                }
                worker.LoadFrames = worker.Frames;
                worker.Frames = 0;
                worker.Phase = Phase.Settle;
                goto case Phase.Settle;

            case Phase.Settle:
                if (worker.Frames++ < SettleFrames)
                    return;
                var texture = target.GetTexture();
                if (texture == null || texture->D3D11ShaderResourceView == null)
                {
                    FinishTile(worker, "Keine Textur");
                    return;
                }
                var args = new TextureModificationArgs
                {
                    Uv1 = new Vector2((float)texture->ActualWidth / texture->AllocatedWidth, (float)texture->ActualHeight / texture->AllocatedHeight),
                    NewWidth = TileWidth,
                    NewHeight = (int)(TileWidth * (float)texture->ActualHeight / texture->ActualWidth),
                };
                worker.CopyTask = Plugin.TextureProvider.CreateFromExistingTextureAsync(new GameTextureWrap(texture), args, leaveWrapOpen: true, debugName: $"GlamourSpike tile {worker.Tile!.ItemId}");
                worker.Phase = Phase.Copy;
                break;

            case Phase.Copy:
                if (worker.CopyTask is not { IsCompleted: true } task)
                    return;
                worker.CopyTask = null;
                if (task.IsCompletedSuccessfully)
                {
                    worker.Tile!.Texture = task.Result;
                    FinishTile(worker, null);
                }
                else
                {
                    FinishTile(worker, task.Exception?.InnerException?.Message ?? "Kopieren fehlgeschlagen");
                }
                break;
        }
    }

    private void FinishTile(Worker worker, string? error)
    {
        var tile = worker.Tile!;
        tile.Error = error;
        tile.Milliseconds = worker.Stopwatch.Elapsed.TotalMilliseconds;
        tile.LoadFrames = worker.LoadFrames;
        finishedTiles++;
        worker.Tile = null;
        worker.Phase = Phase.Apply;
        Plugin.Log.Information($"Tile item {tile.ItemId} '{tile.Name}' on view {tile.ViewIndex}: {tile.Milliseconds:F0} ms, load {tile.LoadFrames} frames, error={error ?? "-"}");
    }

    private void LogTextureInfo()
    {
        var texture = view!.GetTexture();
        if (texture == null)
        {
            Plugin.Log.Warning("Ready but no texture");
            return;
        }
        Plugin.Log.Information($"CharaView texture: actual {texture->ActualWidth}x{texture->ActualHeight}, allocated {texture->AllocatedWidth}x{texture->AllocatedHeight}, format {texture->TextureFormat}, srv {(nint)texture->D3D11ShaderResourceView:X}");
    }

    /// <summary>VRAM taken by the copied tiles (BGRA, 4 bytes per pixel).</summary>
    public long TileBytes => Tiles.Where(t => t.Texture != null).Sum(t => (long)t.Texture!.Width * t.Texture.Height * 4);

    private void DisposeTiles()
    {
        foreach (var tile in Tiles)
        {
            tile.Texture?.Dispose();
            tile.Texture = null;
        }
    }

    public void OnLogout(int type, int code) => Stop();

    public void OnTerritoryChanged(uint territory)
    {
        if (view != null)
        {
            Plugin.Log.Information("Territory changed, releasing CharaView");
            Stop();
            Status = "Gebietswechsel: gestoppt, bitte neu starten";
        }
    }

    public void Dispose()
    {
        Stop();
        DisposeTiles();
    }
}
