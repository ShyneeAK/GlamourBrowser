using GlamourBrowser.Core.Items;
using GlamourBrowser.Core.Localization;

namespace GlamourBrowser.Core.CustomSets;

/// <summary>One character's custom sets, the last changed first.</summary>
/// <param name="variants">"+N" variants mapped to their base item (see <see cref="ItemVariants"/>): a variant is stored as its base.</param>
public sealed class CustomSetStore(ulong contentId, IReadOnlyDictionary<uint, uint>? variants = null)
{
    public const int MaxNameLength = 40;
    public static string DefaultName => Loc.T("Neues Set");

    private readonly List<CustomSet> sets = new();
    private readonly List<CustomFolder> folders = new();

    public ulong ContentId { get; } = contentId;

    /// <summary>The id the next new set gets; ids of deleted sets are not given out again (a wish may still point at one).</summary>
    public uint NextId { get; private set; } = 1;

    public IReadOnlyList<CustomSet> Sets => sets;

    /// <summary>The folders by name.</summary>
    public IReadOnlyList<CustomFolder> Folders => folders;

    /// <summary>The id the next new folder gets.</summary>
    public uint NextFolderId { get; private set; } = 1;

    public event Action? Changed;

    public CustomSet? Find(uint id) => sets.Find(s => s.Id == id);

    /// <param name="folder">The folder the new set goes into (0 for none, also for an unknown folder).</param>
    public CustomSet Add(string name, IReadOnlyList<CustomPiece> pieces, DateTimeOffset now, uint folder = 0)
    {
        var set = Normalize(new CustomSet(NextId++, name, pieces, now) { Folder = FindFolder(folder) != null ? folder : 0 });
        Insert(set);
        Changed?.Invoke();
        return set;
    }

    /// <summary>
    /// Replaces the set with the same id; it stays in its folder. Returns false (and stays quiet) for an unknown id or
    /// the same content.
    /// </summary>
    public bool Update(CustomSet set)
    {
        var old = Find(set.Id);
        if (old == null)
            return false;
        set = Normalize(set with { Folder = old.Folder });
        if (old.SameContent(set))
            return false;

        sets.Remove(old);
        Insert(set);
        Changed?.Invoke();
        return true;
    }

    public bool Remove(uint id)
    {
        if (sets.RemoveAll(s => s.Id == id) == 0)
            return false;
        Changed?.Invoke();
        return true;
    }

    public CustomFolder? FindFolder(uint id) => folders.Find(f => f.Id == id);

    public CustomFolder AddFolder(string name)
    {
        var folder = new CustomFolder(NextFolderId++, FolderName(name));
        InsertFolder(folder);
        Changed?.Invoke();
        return folder;
    }

    public bool RenameFolder(uint id, string name)
    {
        if (FindFolder(id) is not { } old || old.Name == FolderName(name))
            return false;
        folders.Remove(old);
        InsertFolder(old with { Name = FolderName(name) });
        Changed?.Invoke();
        return true;
    }

    /// <summary>Deletes the folder; its sets stay, outside any folder.</summary>
    public bool RemoveFolder(uint id)
    {
        if (folders.RemoveAll(f => f.Id == id) == 0)
            return false;
        for (var i = 0; i < sets.Count; i++)
        {
            if (sets[i].Folder == id)
                sets[i] = sets[i] with { Folder = 0 };
        }
        Changed?.Invoke();
        return true;
    }

    /// <summary>Puts the set into the folder (0: out of any folder); its place in the list and its time stay.</summary>
    public bool Move(uint setId, uint folder)
    {
        var index = sets.FindIndex(s => s.Id == setId);
        if (index < 0 || folder != 0 && FindFolder(folder) == null || sets[index].Folder == folder)
            return false;
        sets[index] = sets[index] with { Folder = folder };
        Changed?.Invoke();
        return true;
    }

    /// <summary>Takes a folder from the file as it is, without <see cref="Changed"/>.</summary>
    public void RestoreFolder(CustomFolder folder, uint nextFolderId)
    {
        folders.RemoveAll(f => f.Id == folder.Id);
        InsertFolder(folder with { Name = FolderName(folder.Name) });
        NextFolderId = Math.Max(Math.Max(NextFolderId, nextFolderId), folder.Id + 1);
    }

    /// <summary>Sets in a folder the file does not have go out of it (after the folders and sets are restored).</summary>
    public void DropUnknownFolders()
    {
        for (var i = 0; i < sets.Count; i++)
        {
            if (sets[i].Folder != 0 && FindFolder(sets[i].Folder) == null)
                sets[i] = sets[i] with { Folder = 0 };
        }
    }

    /// <summary>Ids below <paramref name="nextId"/> were given out before (in a file that broke): new sets get none of them.</summary>
    public void ReserveIdsBelow(uint nextId) => NextId = Math.Max(NextId, nextId);

    /// <summary>Takes a set from the file as it is (id and time kept), without <see cref="Changed"/>.</summary>
    public void Restore(CustomSet set, uint nextId)
    {
        set = Normalize(set);
        sets.RemoveAll(s => s.Id == set.Id);
        Insert(set);
        NextId = Math.Max(Math.Max(NextId, nextId), set.Id + 1);
    }

    private void InsertFolder(CustomFolder folder)
    {
        var comparer = Sources.NaturalComparer.ForLanguage();
        var index = folders.FindIndex(f => comparer.Compare(f.Name, folder.Name) > 0);
        folders.Insert(index < 0 ? folders.Count : index, folder);
    }

    private static string FolderName(string name)
    {
        name = name.Trim();
        if (name.Length > MaxNameLength)
            name = name[..MaxNameLength].TrimEnd();
        return name.Length == 0 ? DefaultFolderName : name;
    }

    public static string DefaultFolderName => Loc.T("Neuer Ordner");

    private void Insert(CustomSet set)
    {
        var index = sets.FindIndex(s => s.Changed < set.Changed || s.Changed == set.Changed && s.Id < set.Id);
        sets.Insert(index < 0 ? sets.Count : index, set);
    }

    /// <summary>Base items for variants, one piece per slot (the last wins) in slot order, no empty items; a trimmed name.</summary>
    private CustomSet Normalize(CustomSet set)
    {
        var name = set.Name.Trim();
        if (name.Length > MaxNameLength)
            name = name[..MaxNameLength].TrimEnd();
        var pieces = set.Pieces
            .Where(p => p.ItemId != 0)
            .Select(p => variants != null && variants.TryGetValue(p.ItemId, out var baseId) ? p with { ItemId = baseId } : p)
            .GroupBy(p => p.Slot)
            .Select(g => g.Last())
            .OrderBy(p => p.Slot)
            .ToList();
        return set with { Name = name.Length == 0 ? DefaultName : name, Pieces = pieces };
    }
}
