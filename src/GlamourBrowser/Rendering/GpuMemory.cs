using System;
using TerraFX.Interop.DirectX;
using TerraFX.Interop.Windows;
using static TerraFX.Interop.Windows.Windows;

namespace GlamourBrowser.Rendering;

/// <summary>
/// The game's video memory through DXGI (under Wine DXVK answers it): what the process uses and the budget it has.
/// The adapter is looked up once; without one (no device yet, an old driver) the figures stay unknown.
/// </summary>
public sealed unsafe class GpuMemory : IDisposable
{
    private IDXGIAdapter3* adapter;
    private bool unavailable;

    /// <summary>Used and budget of the local (dedicated) video memory, null when DXGI cannot tell.</summary>
    public (long Used, long Budget)? Query()
    {
        if (adapter == null && !TryFindAdapter())
            return null;

        DXGI_QUERY_VIDEO_MEMORY_INFO info;
        if (FAILED(adapter->QueryVideoMemoryInfo(0, DXGI_MEMORY_SEGMENT_GROUP.DXGI_MEMORY_SEGMENT_GROUP_LOCAL, &info)))
            return null;
        return ((long)info.CurrentUsage, (long)info.Budget);
    }

    /// <summary>Bytes per pixel of a texture behind a shader resource view (the tile copies), 4 when unknown.</summary>
    public static int BytesPerPixel(nint shaderResourceView)
    {
        if (shaderResourceView == 0)
            return 4;
        D3D11_SHADER_RESOURCE_VIEW_DESC desc;
        ((ID3D11ShaderResourceView*)shaderResourceView)->GetDesc(&desc);
        return desc.Format switch
        {
            >= DXGI_FORMAT.DXGI_FORMAT_R32G32B32A32_TYPELESS and <= DXGI_FORMAT.DXGI_FORMAT_R32G32B32A32_SINT => 16,
            >= DXGI_FORMAT.DXGI_FORMAT_R16G16B16A16_TYPELESS and <= DXGI_FORMAT.DXGI_FORMAT_R16G16B16A16_SINT => 8,
            _ => 4,
        };
    }

    private bool TryFindAdapter()
    {
        if (unavailable)
            return false;

        var device = (IUnknown*)Plugin.PluginInterface.UiBuilder.DeviceHandle;
        // No device yet: try again on the next sample.
        if (device == null)
            return false;

        IDXGIDevice* dxgiDevice = null;
        IDXGIAdapter* baseAdapter = null;
        try
        {
            IDXGIAdapter3* found = null;
            if (FAILED(device->QueryInterface(__uuidof<IDXGIDevice>(), (void**)&dxgiDevice))
                || FAILED(dxgiDevice->GetAdapter(&baseAdapter))
                || FAILED(baseAdapter->QueryInterface(__uuidof<IDXGIAdapter3>(), (void**)&found)))
            {
                unavailable = true;
                return false;
            }
            adapter = found;
            return true;
        }
        finally
        {
            if (baseAdapter != null)
                baseAdapter->Release();
            if (dxgiDevice != null)
                dxgiDevice->Release();
        }
    }

    public void Dispose()
    {
        if (adapter != null)
            adapter->Release();
        adapter = null;
    }
}
