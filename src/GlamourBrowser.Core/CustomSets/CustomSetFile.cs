using System.Text.Json;
using System.Text.RegularExpressions;
using GlamourBrowser.Core.Items;

namespace GlamourBrowser.Core.CustomSets;

/// <summary>Loads and saves one character's <see cref="CustomSetStore"/> as JSON.</summary>
public static class CustomSetFile
{
    private static readonly JsonSerializerOptions Options = new() { WriteIndented = true };
    private static readonly Regex IdField = new("\"(Id|NextId)\"\\s*:\\s*(\\d+)", RegexOptions.CultureInvariant);

    /// <summary>
    /// A broken file (also a piece in no plate slot), or one of another character, moves to <c>.bak</c> and the list
    /// starts empty; its new sets get none of the ids the file still shows (a wish may point at one). Unknown items are kept.
    /// </summary>
    /// <param name="variants">"+N" variants mapped to their base item, see <see cref="CustomSetStore"/>.</param>
    public static CustomSetStore Load(string path, ulong contentId, IReadOnlyDictionary<uint, uint>? variants = null)
    {
        var store = new CustomSetStore(contentId, variants);
        if (!File.Exists(path))
            return store;

        var text = File.ReadAllText(path);
        try
        {
            var dto = JsonSerializer.Deserialize<FileDto>(text, Options)
                      ?? throw new JsonException("empty document");
            if (dto.ContentId != contentId)
                throw new JsonException($"file of character {dto.ContentId}");
            foreach (var set in dto.Sets ?? throw new JsonException("no sets"))
            {
                if (set?.Pieces == null || set.Pieces.Contains(null))
                    throw new JsonException("empty set or piece");
                if (set.Pieces.FirstOrDefault(p => !CustomSetSubjects.Slots.Contains(p!.Slot)) is { } wrong)
                    throw new JsonException($"piece in slot {(int)wrong.Slot}");
                var pieces = set.Pieces.Select(p => new CustomPiece(p!.Slot, p.ItemId, p.Stain0, p.Stain1)).ToList();
                store.Restore(new CustomSet(set.Id, set.Name ?? "", pieces, set.Changed) { Folder = set.Folder }, dto.NextId);
            }
            foreach (var folder in dto.Folders ?? [])
            {
                if (folder == null)
                    throw new JsonException("empty folder");
                store.RestoreFolder(new CustomFolder(folder.Id, folder.Name ?? ""), dto.NextFolderId);
            }
            store.DropUnknownFolders();
            return store;
        }
        catch (Exception e) when (e is JsonException or NotSupportedException)
        {
            File.Move(path, path + ".bak", overwrite: true);
            var empty = new CustomSetStore(contentId, variants);
            empty.ReserveIdsBelow(NextIdAfter(text));
            return empty;
        }
    }

    /// <summary>The first id past every set id and counter the (broken) text shows, 1 for none.</summary>
    private static uint NextIdAfter(string text)
    {
        var next = 1u;
        foreach (Match match in IdField.Matches(text))
        {
            if (!uint.TryParse(match.Groups[2].Value, out var value))
                continue;
            // A counter is the next id itself, a set id is taken.
            next = Math.Max(next, match.Groups[1].Value == "NextId" || value == uint.MaxValue ? value : value + 1);
        }
        return next;
    }

    /// <summary>Writes to a temporary file first so a concurrent reader never sees half a file.</summary>
    public static void Save(CustomSetStore store, string path)
    {
        var dto = new FileDto(store.ContentId, store.NextId, store.Sets
            .Select(s => new SetDto(s.Id, s.Name, s.Changed, s.Pieces.Select(p => new PieceDto(p.Slot, p.ItemId, p.Stain0, p.Stain1)).ToList<PieceDto?>(), s.Folder))
            .ToList<SetDto?>(), store.NextFolderId, store.Folders.Select(f => new FolderDto(f.Id, f.Name)).ToList<FolderDto?>());

        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        var tmp = path + ".tmp";
        File.WriteAllText(tmp, JsonSerializer.Serialize(dto, Options));
        File.Move(tmp, path, overwrite: true);
    }

    // Folders came later: a file without them has none, its sets are in none.
    private sealed record FileDto(ulong ContentId, uint NextId, List<SetDto?>? Sets, uint NextFolderId = 1, List<FolderDto?>? Folders = null);

    private sealed record SetDto(uint Id, string? Name, DateTimeOffset Changed, List<PieceDto?>? Pieces, uint Folder = 0);

    private sealed record FolderDto(uint Id, string? Name);

    private sealed record PieceDto(EquipSlot Slot, uint ItemId, byte Stain0, byte Stain1);
}
