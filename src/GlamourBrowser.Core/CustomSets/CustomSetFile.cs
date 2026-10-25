using System.Text.Json;
using GlamourBrowser.Core.Items;

namespace GlamourBrowser.Core.CustomSets;

/// <summary>Loads and saves one character's <see cref="CustomSetStore"/> as JSON.</summary>
public static class CustomSetFile
{
    private static readonly JsonSerializerOptions Options = new() { WriteIndented = true };

    /// <summary>A broken file, or one of another character, moves to <c>.bak</c> and the list starts empty. Unknown items are kept.</summary>
    /// <param name="variants">"+N" variants mapped to their base item, see <see cref="CustomSetStore"/>.</param>
    public static CustomSetStore Load(string path, ulong contentId, IReadOnlyDictionary<uint, uint>? variants = null)
    {
        var store = new CustomSetStore(contentId, variants);
        if (!File.Exists(path))
            return store;

        try
        {
            var dto = JsonSerializer.Deserialize<FileDto>(File.ReadAllText(path), Options)
                      ?? throw new JsonException("empty document");
            if (dto.ContentId != contentId)
                throw new JsonException($"file of character {dto.ContentId}");
            foreach (var set in dto.Sets ?? throw new JsonException("no sets"))
            {
                if (set?.Pieces == null || set.Pieces.Contains(null))
                    throw new JsonException("empty set or piece");
                var pieces = set.Pieces.Select(p => new CustomPiece(p!.Slot, p.ItemId, p.Stain0, p.Stain1)).ToList();
                store.Restore(new CustomSet(set.Id, set.Name ?? "", pieces, set.Changed), dto.NextId);
            }
            return store;
        }
        catch (Exception e) when (e is JsonException or NotSupportedException)
        {
            File.Move(path, path + ".bak", overwrite: true);
            return new CustomSetStore(contentId, variants);
        }
    }

    /// <summary>Writes to a temporary file first so a concurrent reader never sees half a file.</summary>
    public static void Save(CustomSetStore store, string path)
    {
        var dto = new FileDto(store.ContentId, store.NextId, store.Sets
            .Select(s => new SetDto(s.Id, s.Name, s.Changed, s.Pieces.Select(p => new PieceDto(p.Slot, p.ItemId, p.Stain0, p.Stain1)).ToList<PieceDto?>()))
            .ToList<SetDto?>());

        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        var tmp = path + ".tmp";
        File.WriteAllText(tmp, JsonSerializer.Serialize(dto, Options));
        File.Move(tmp, path, overwrite: true);
    }

    private sealed record FileDto(ulong ContentId, uint NextId, List<SetDto?>? Sets);

    private sealed record SetDto(uint Id, string? Name, DateTimeOffset Changed, List<PieceDto?>? Pieces);

    private sealed record PieceDto(EquipSlot Slot, uint ItemId, byte Stain0, byte Stain1);
}
