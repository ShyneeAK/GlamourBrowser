using GlamourBrowser.Core.CustomSets;
using GlamourBrowser.Core.Items;
using GlamourBrowser.Core.Sets;

namespace GlamourBrowser.Core.Tests;

public class CustomSetFoldersTests
{
    private static GlamourItem Item(uint id, EquipSlot slot) => new(id, $"Item {id}", 0, slot, new HashSet<uint>(), 50, 2, RaceGenderRestriction.None);

    private static SetSubject Set(uint id) => new(new GlamourSet(new SetId(SetKind.Custom, id), $"Set {id}", [], 50), [Item(id * 10, EquipSlot.Body)]);

    private static readonly CustomFolder Raid = new(1, "Raid");
    private static readonly CustomFolder Alltag = new(2, "Alltag");
    private static readonly IReadOnlyList<SetSubject> Sets = [Set(1), Set(2), Set(3), Set(4)];

    // Sets 2 and 3 in "Raid", 4 in a folder that no longer exists.
    private static uint FolderOf(uint id) => id switch { 2 or 3 => 1, 4 => 9, _ => 0 };

    [Fact]
    public void View_AtTheTopTheFoldersThenTheSetsInNone()
    {
        var view = CustomSetFolders.View(Sets, [Alltag, Raid], FolderOf, open: 0, searching: false);

        Assert.Equal(["Alltag", "Raid", "Set 1", "Set 4"], view.Select(s => s.Name));
        var raid = Assert.IsType<FolderSubject>(view[1]);
        Assert.Equal("Set 2", raid.Cover?.Name);
        Assert.Equal(["Set 2", "Set 3"], raid.Covers.Select(c => c.Name));
        Assert.Null(Assert.IsType<FolderSubject>(view[0]).Cover);
    }

    [Fact]
    public void View_AnOpenFolderShowsItsSets()
        => Assert.Equal(["Set 2", "Set 3"], CustomSetFolders.View(Sets, [Alltag, Raid], FolderOf, open: 1, searching: false).Select(s => s.Name));

    [Fact]
    public void View_ASearchAtTheTopFindsTheSetsOfEveryFolder()
        => Assert.Equal(["Set 1", "Set 2", "Set 3", "Set 4"], CustomSetFolders.View(Sets, [Alltag, Raid], FolderOf, open: 0, searching: true).Select(s => s.Name));

    [Fact]
    public void Covers_AtMostTheFirstFour()
    {
        var folder = new FolderSubject(Raid, [Set(1), Set(2), Set(3), Set(4), Set(5)]);
        Assert.Equal(["Set 1", "Set 2", "Set 3", "Set 4"], folder.Covers.Select(c => c.Name));
    }
}
