using GlamourBrowser.Core.Items;
using Lumina;
using Lumina.Excel.Sheets;

namespace GlamourBrowser.Sources;

public static class VariantSheet
{
    /// <summary>The "+N" variants of equippable items mapped to their base item (see <see cref="ItemVariants"/>).</summary>
    public static IReadOnlyDictionary<uint, uint> BaseOf(GameData data)
        => ItemVariants.BaseOf(data.GetExcelSheet<Item>()!
            .Where(item => item.ModelMain != 0 && !item.Name.IsEmpty && item.EquipSlotCategory.RowId != 0)
            .Select(item => new VariantCandidate(item.RowId, item.Name.ExtractText(), item.ModelMain, item.ModelSub)));
}
