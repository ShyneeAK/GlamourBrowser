using System.Text.Json;

namespace GlamourBrowser.Core.Ownership;

/// <summary>Loads and saves one character's <see cref="OwnershipStore"/> as JSON.</summary>
public static class OwnershipFile
{
    private static readonly JsonSerializerOptions Options = new() { WriteIndented = true };

    /// <param name="variants">"+N" variants mapped to their base item, see <see cref="OwnershipStore"/>.</param>
    public static OwnershipStore Load(string path, ulong contentId, IReadOnlyDictionary<uint, uint>? variants = null)
    {
        var store = new OwnershipStore(contentId, variants);
        if (!File.Exists(path))
            return store;

        try
        {
            var dto = JsonSerializer.Deserialize<FileDto>(File.ReadAllText(path), Options)
                      ?? throw new JsonException("empty document");
            foreach (var c in dto.Containers)
                store.ReplaceContainer(new ContainerSnapshot(new ContainerId(c.Kind, c.Key), c.Name ?? LegacyName(c.Kind, c.DisplayName), c.Seen, c.ItemIds)
                {
                    Entries = c.Entries?.Select(e => new DresserEntry(e.Id, e.Stain0, e.Stain1)).ToList() ?? [],
                });
            return store;
        }
        catch (Exception e) when (e is JsonException or NotSupportedException)
        {
            File.Move(path, path + ".bak", overwrite: true);
            return new OwnershipStore(contentId, variants);
        }
    }

    /// <summary>Writes to a temporary file first so a concurrent reader never sees half a file.</summary>
    public static void Save(OwnershipStore store, string path)
    {
        var dto = new FileDto(store.ContentId, store.Containers
            .Select(c => new ContainerDto(c.Id.Kind, c.Id.Key, c.Name, c.DisplayName, c.Seen, c.ItemIds.ToList(),
                                          c.Entries.Count > 0 ? c.Entries.Select(e => new EntryDto(e.RawId, e.Stain0, e.Stain1)).ToList() : null))
            .ToList());

        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        var tmp = path + ".tmp";
        File.WriteAllText(tmp, JsonSerializer.Serialize(dto, Options));
        File.Move(tmp, path, overwrite: true);
    }

    /// <summary>
    /// Older files kept the shown name, in the language of the read ("Gehilfe Mira", "Kommode"): a retainer keeps the word
    /// after the last space, its own name (retainer names have no space), the other containers need none.
    /// </summary>
    private static string LegacyName(ContainerKind kind, string? displayName)
        => kind == ContainerKind.Retainer && displayName != null ? displayName[(displayName.LastIndexOf(' ') + 1)..] : "";

    private sealed record FileDto(ulong ContentId, List<ContainerDto> Containers);

    /// <param name="DisplayName">Read only from older files, see <see cref="LegacyName"/>; still written for older plugin versions.</param>
    /// <param name="Entries">The dresser's entries with their dyes; missing in older files and for the other containers.</param>
    private sealed record ContainerDto(ContainerKind Kind, string Key, string? Name, string? DisplayName, DateTimeOffset Seen, List<uint> ItemIds,
                                       List<EntryDto>? Entries = null);

    private sealed record EntryDto(uint Id, byte Stain0, byte Stain1);
}
