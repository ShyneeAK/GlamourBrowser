using System.Text.Json;
using GlamourBrowser.Core.Sets;

namespace GlamourBrowser.Core.Wishlist;

/// <summary>Loads and saves one character's <see cref="WishlistStore"/> as JSON.</summary>
public static class WishlistFile
{
    private static readonly JsonSerializerOptions Options = new() { WriteIndented = true };

    /// <summary>
    /// A broken file, or one of another character, moves to <c>.bak</c> and the list starts empty. Entries the catalog
    /// does not know are kept; a variant and its base listed both become one entry with the earlier date.
    /// </summary>
    /// <param name="variants">"+N" variants mapped to their base item, see <see cref="WishlistStore"/>.</param>
    public static WishlistStore Load(string path, ulong contentId, IReadOnlyDictionary<uint, uint>? variants = null)
    {
        var store = new WishlistStore(contentId, variants);
        if (!File.Exists(path))
            return store;

        try
        {
            var dto = JsonSerializer.Deserialize<FileDto>(File.ReadAllText(path), Options)
                      ?? throw new JsonException("empty document");
            if (dto.ContentId != contentId)
                throw new JsonException($"file of character {dto.ContentId}");
            // Earliest first, so that of two entries merging into one base the earlier date stays.
            foreach (var e in (dto.Entries ?? throw new JsonException("no entries")).OrderBy(e => e.Added))
                store.Add(new WishKey(e.Kind, e.ItemId, new SetId(e.SetKind, e.SetValue)), e.Added);
            return store;
        }
        catch (Exception e) when (e is JsonException or NotSupportedException)
        {
            File.Move(path, path + ".bak", overwrite: true);
            return new WishlistStore(contentId, variants);
        }
    }

    /// <summary>Writes to a temporary file first so a concurrent reader never sees half a file.</summary>
    public static void Save(WishlistStore store, string path)
    {
        var dto = new FileDto(store.ContentId, store.Entries
            .Select(e => new EntryDto(e.Key.Kind, e.Key.ItemId, e.Key.Set.Kind, e.Key.Set.Value, e.Added))
            .ToList());

        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        var tmp = path + ".tmp";
        File.WriteAllText(tmp, JsonSerializer.Serialize(dto, Options));
        File.Move(tmp, path, overwrite: true);
    }

    private sealed record FileDto(ulong ContentId, List<EntryDto>? Entries);

    private sealed record EntryDto(WishKind Kind, uint ItemId, SetKind SetKind, uint SetValue, DateTimeOffset Added);
}
