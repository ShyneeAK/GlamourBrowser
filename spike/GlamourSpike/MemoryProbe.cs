using System;
using System.Collections.Generic;
using System.Diagnostics;
using TerraFX.Interop.DirectX;
using TerraFX.Interop.Windows;
using static TerraFX.Interop.Windows.Windows;

namespace GlamourSpike;

public sealed record MemorySample(string Label, long PrivateBytes, long WorkingSet, long VramLocal, long VramNonLocal);

/// <summary>Process RAM and DXGI video memory usage snapshots. Under DXVK the VRAM numbers cover the whole game process.</summary>
public sealed unsafe class MemoryProbe
{
    public List<MemorySample> Samples { get; } = new();

    public void Clear() => Samples.Clear();

    public void Snapshot(string label)
    {
        using var process = Process.GetCurrentProcess();
        var (local, nonLocal) = QueryVram();
        var sample = new MemorySample(label, process.PrivateMemorySize64, process.WorkingSet64, local, nonLocal);
        Samples.Add(sample);
        Plugin.Log.Information($"Memory [{label}]: private {Mb(sample.PrivateBytes)}, working set {Mb(sample.WorkingSet)}, VRAM local {Mb(local)}, non-local {Mb(nonLocal)}");
    }

    public static string Mb(long bytes) => bytes < 0 ? "n/a" : $"{bytes / (1024.0 * 1024.0):F1} MB";

    private static (long Local, long NonLocal) QueryVram()
    {
        try
        {
            var device = (ID3D11Device*)Plugin.PluginInterface.UiBuilder.DeviceHandle;
            if (device == null)
                return (-1, -1);

            using ComPtr<IDXGIDevice> dxgiDevice = default;
            if (device->QueryInterface(__uuidof<IDXGIDevice>(), (void**)dxgiDevice.GetAddressOf()).FAILED)
                return (-1, -1);

            using ComPtr<IDXGIAdapter> adapter = default;
            if (dxgiDevice.Get()->GetAdapter(adapter.GetAddressOf()).FAILED)
                return (-1, -1);

            using ComPtr<IDXGIAdapter3> adapter3 = default;
            if (adapter.Get()->QueryInterface(__uuidof<IDXGIAdapter3>(), (void**)adapter3.GetAddressOf()).FAILED)
                return (-1, -1);

            DXGI_QUERY_VIDEO_MEMORY_INFO local, nonLocal;
            adapter3.Get()->QueryVideoMemoryInfo(0, DXGI_MEMORY_SEGMENT_GROUP.DXGI_MEMORY_SEGMENT_GROUP_LOCAL, &local);
            adapter3.Get()->QueryVideoMemoryInfo(0, DXGI_MEMORY_SEGMENT_GROUP.DXGI_MEMORY_SEGMENT_GROUP_NON_LOCAL, &nonLocal);
            return ((long)local.CurrentUsage, (long)nonLocal.CurrentUsage);
        }
        catch (Exception e)
        {
            Plugin.Log.Warning(e, "VRAM query failed");
            return (-1, -1);
        }
    }
}
