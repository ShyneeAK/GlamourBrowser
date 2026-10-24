namespace GlamourBrowser.Core.Rendering;

/// <summary>
/// Rendered tile textures. After <see cref="Retain"/> only the retained keys are kept;
/// textures added for other keys (late results of an old page) are disposed right away.
/// </summary>
public sealed class TileCache<T> : IDisposable where T : class, IDisposable
{
    private readonly Dictionary<TileKey, T> textures = new();
    private IReadOnlySet<TileKey>? keep;

    public int Count => textures.Count;

    public bool TryGet(TileKey key, out T? texture) => textures.TryGetValue(key, out texture);

    public void Add(TileKey key, T texture)
    {
        if (keep != null && !keep.Contains(key))
        {
            texture.Dispose();
            return;
        }

        if (textures.Remove(key, out var old) && !ReferenceEquals(old, texture))
            old.Dispose();
        textures[key] = texture;
    }

    public void Retain(IReadOnlySet<TileKey> keys)
    {
        keep = keys;
        foreach (var key in textures.Keys.Where(k => !keys.Contains(k)).ToList())
        {
            textures[key].Dispose();
            textures.Remove(key);
        }
    }

    public void Clear()
    {
        foreach (var texture in textures.Values)
            texture.Dispose();
        textures.Clear();
    }

    public void Dispose() => Clear();
}
