using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using Dalamud.Interface.Textures.TextureWraps;
using GlamourBrowser.Core.Rendering;
using GlamourBrowser.Core.Ui;
using GlamourBrowser.Core.Localization;

namespace GlamourBrowser.Rendering;

/// <summary>The debug memory line under the tile grid (setting "Speicheranzeige"), sampled at most once a second.</summary>
public sealed class MemoryStats : IDisposable
{
    private const long SampleInterval = 1000;

    public static string Tooltip => Loc.T(
        "Kachel-Cache: gerenderte Kacheln dieses Plugins (aktuelle, vorausgerenderte und zuletzt gesehene Seiten), Größe geschätzt.\n"
        + "VRAM: Grafikspeicher, den das ganze Spiel belegt / den es nutzen darf.\n"
        + "RAM: Arbeitsspeicher des Spielprozesses.\n"
        + ".NET: verwalteter Speicher von Dalamud und allen Plugins zusammen.");

    private readonly TileCache<IDalamudTextureWrap> cache;
    // Created on the first sample: a missing DirectX interop must not break the plugin, only the VRAM figure.
    private GpuMemory? gpu;
    private bool gpuFailed;
    private long nextSample;

    public MemoryStats(TileCache<IDalamudTextureWrap> cache) => this.cache = cache;

    public string Line { get; private set; } = "";

    /// <summary>Takes a new sample when the last one is a second old; with <see cref="RenderSync.Lock"/> held (the cache is shared).</summary>
    public void Update()
    {
        var now = Environment.TickCount64;
        if (now < nextSample)
            return;
        nextSample = now + SampleInterval;

        var bytesPerPixel = 0;
        var tileBytes = cache.Sum(t =>
        {
            // All tiles are copies of the same render target, so one format stands for all.
            if (bytesPerPixel == 0)
                bytesPerPixel = BytesPerPixel(t);
            return (long)t.Width * t.Height * bytesPerPixel;
        });
        var vram = QueryGpu() is { } video ? $"{ByteSize.Format(video.Used)} / {ByteSize.Format(video.Budget)}" : "?";
        using var process = Process.GetCurrentProcess();
        Line = Loc.F("Kachel-Cache: {0} Kacheln ≈ {1}", cache.Count, ByteSize.Format(tileBytes)) + $"  ·  VRAM {vram}  ·  RAM {ByteSize.Format(process.WorkingSet64)}"
               + $"  ·  .NET {ByteSize.Format(GC.GetTotalMemory(false))}";
    }

    private (long Used, long Budget)? QueryGpu()
    {
        if (gpuFailed)
            return null;
        try
        {
            return QueryGpuCore();
        }
        catch (Exception e)
        {
            gpuFailed = true;
            Plugin.Log.Warning(e, "VRAM figures unavailable");
            return null;
        }
    }

    private int BytesPerPixel(IDalamudTextureWrap texture)
    {
        if (gpuFailed)
            return 4;
        try
        {
            return BytesPerPixelCore(texture);
        }
        catch (Exception e)
        {
            gpuFailed = true;
            Plugin.Log.Warning(e, "Tile texture format unavailable");
            return 4;
        }
    }

    // Separate methods: if the DirectX interop cannot load, only their JIT fails, inside the try above.
    [MethodImpl(MethodImplOptions.NoInlining)]
    private (long Used, long Budget)? QueryGpuCore() => (gpu ??= new GpuMemory()).Query();

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static int BytesPerPixelCore(IDalamudTextureWrap texture) => GpuMemory.BytesPerPixel((nint)texture.Handle.Handle);

    public void Dispose() => DisposeGpu();

    [MethodImpl(MethodImplOptions.NoInlining)]
    private void DisposeGpu()
    {
        gpu?.Dispose();
        gpu = null;
    }
}
