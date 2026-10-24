using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Textures.TextureWraps;
using FFXIVClientStructs.FFXIV.Client.Graphics.Kernel;

namespace GlamourBrowser.Rendering;

/// <summary>
/// Non-owning wrap around a game texture so it can be passed to
/// ITextureProvider.CreateFromExistingTextureAsync. Dispose does nothing.
/// </summary>
public sealed unsafe class GameTextureWrap : IDalamudTextureWrap
{
    public GameTextureWrap(Texture* texture)
    {
        Handle = new ImTextureID(texture->D3D11ShaderResourceView);
        Width = (int)texture->AllocatedWidth;
        Height = (int)texture->AllocatedHeight;
    }

    public ImTextureID Handle { get; }
    public int Width { get; }
    public int Height { get; }

    public void Dispose()
    {
    }
}
