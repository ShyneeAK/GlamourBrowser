using GlamourBrowser.Core.Items;
using GlamourBrowser.Core.Sets;

namespace GlamourBrowser.Core.CustomSets;

/// <summary>A folder's tile in "Meine Sets": it shows its first sets as a collage (<see cref="Covers"/>); a click opens it.</summary>
/// <param name="Sets">The folder's sets in the order "Meine Sets" shows them.</param>
public sealed record FolderSubject(CustomFolder Folder, IReadOnlyList<SetSubject> Sets) : TileSubject
{
    public override (uint ItemId, SetId Set) Id => (0, new SetId(SetKind.CustomFolder, Folder.Id));
    public override string Name => Folder.Name;

    /// <summary>The most sets the folder's tile shows.</summary>
    public const int MaxCovers = 4;

    /// <summary>The first set; null for an empty folder.</summary>
    public SetSubject? Cover => Sets.Count > 0 ? Sets[0] : null;

    /// <summary>The sets whose renders the folder's tile shows, 2×2: the first <see cref="MaxCovers"/>.</summary>
    public IReadOnlyList<SetSubject> Covers => Sets.Take(MaxCovers).ToList();

    public override IReadOnlyList<GlamourItem> Wear => Cover?.Wear ?? [];
    public override EquipSlot CameraSlot => EquipSlot.Body;
}

public static class CustomSetFolders
{
    /// <summary>
    /// What "Meine Sets" shows: at the top the folders (by name) and then the sets in none; in an open folder its sets.
    /// A search at the top finds the sets of every folder.
    /// </summary>
    /// <param name="sets">The custom sets in their order, already searched.</param>
    /// <param name="folderOf">The folder of a custom set id (0 for none).</param>
    /// <param name="open">The open folder; 0 for the top.</param>
    public static IReadOnlyList<TileSubject> View(IReadOnlyList<SetSubject> sets, IReadOnlyList<CustomFolder> folders, Func<uint, uint> folderOf,
                                                  uint open, bool searching)
    {
        if (open != 0)
            return sets.Where(s => folderOf(s.Set.Id.Value) == open).ToList<TileSubject>();
        if (searching)
            return sets.ToList<TileSubject>();
        var known = folders.Select(f => f.Id).ToHashSet();
        var tiles = folders.Select(f => (TileSubject)new FolderSubject(f, sets.Where(s => folderOf(s.Set.Id.Value) == f.Id).ToList())).ToList();
        tiles.AddRange(sets.Where(s => !known.Contains(folderOf(s.Set.Id.Value))));
        return tiles;
    }
}
