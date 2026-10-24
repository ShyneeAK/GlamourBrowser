using System.Text.Json;

namespace GlamourBrowser.Core.Ownership;

/// <summary>Loads and saves one character's <see cref="OwnershipStore"/> as JSON.</summary>
public static class OwnershipFile
{
    private static readonly JsonSerializerOptions Options = new() { WriteIndented = true };

    public static OwnershipStore Load(string path, ulong contentId)
    {
        var store = new OwnershipStore(contentId);
        if (!File.Exists(path))
            return store;

        try
        {
            var dto = JsonSerializer.Deserialize<FileDto>(File.ReadAllText(path), Options)
                      ?? throw new JsonException("empty document");
            foreach (var c in dto.Containers)
                store.ReplaceContainer(new ContainerSnapshot(new ContainerId(c.Kind, c.Key), c.DisplayName, c.Seen, c.ItemIds));
            return store;
        }
        catch (Exception e) when (e is JsonException or NotSupportedException)
        {
            File.Move(path, path + ".bak", overwrite: true);
            return new OwnershipStore(contentId);
        }
    }

    /// <summary>Writes to a temporary file first so a concurrent reader never sees half a file.</summary>
    public static void Save(OwnershipStore store, string path)
    {
        var dto = new FileDto(store.ContentId, store.Containers
            .Select(c => new ContainerDto(c.Id.Kind, c.Id.Key, c.DisplayName, c.Seen, c.ItemIds.ToList()))
            .ToList());

        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        var tmp = path + ".tmp";
        File.WriteAllText(tmp, JsonSerializer.Serialize(dto, Options));
        File.Move(tmp, path, overwrite: true);
    }

    private sealed record FileDto(ulong ContentId, List<ContainerDto> Containers);

    private sealed record ContainerDto(ContainerKind Kind, string Key, string DisplayName, DateTimeOffset Seen, List<uint> ItemIds);
}
