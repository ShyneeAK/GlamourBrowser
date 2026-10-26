using GlamourBrowser.Core.Items;
using GlamourBrowser.Core.Localization;
using GlamourBrowser.Core.Ownership;

namespace GlamourBrowser.Core.CustomSets;

public enum PlateSourceKind
{
    Dresser,
    Armoire,
}

/// <summary>Where the plate takes a piece from.</summary>
/// <param name="Id">Index in the glamour dresser, or the row of the armoire (Cabinet sheet).</param>
/// <param name="ItemId">The id the game expects there: the raw dresser id (HQ offset kept), a variant, or one piece of an outfit.</param>
/// <param name="Stain0">The dye the piece is stored with; the plate starts with it (a dye passed with the item is not saved).</param>
public readonly record struct PlateSource(PlateSourceKind Kind, uint Id, uint ItemId, byte Stain0, byte Stain1);

/// <param name="Piece">The set's piece for this slot; null clears the slot.</param>
/// <param name="Missing">Why a piece cannot be transferred ("liegt in: …", "nicht im Besitz").</param>
public sealed record PlateSlot(EquipSlot Slot, CustomPiece? Piece, PlateSource? Source, string? Missing);

/// <summary>A dye the plate editor puts on a channel when the plate is saved (the player pays then): the dye and the items that give it.</summary>
public readonly record struct PlateDye(byte Stain, IReadOnlyList<uint> ItemIds);

public static class PlateDyes
{
    /// <summary>Terebinthe-Öl: what the game itself takes to remove a dye in the plate editor (seen in the dye spike).</summary>
    public const uint RemoverItemId = 5728;

    /// <summary>The dye to put on each channel so the plate gets the set's dyes; null for a channel that already has it or that the item does not have.</summary>
    /// <param name="dyeCount">The item's dye channels (0–2).</param>
    /// <param name="isDye">Whether a stored value is a dye (Stain sheet, or 0 for none).</param>
    /// <param name="dyeItemsOf">The items that give a dye (Stain sheet), e.g. "Einfacher Farbstoff".</param>
    public static (PlateDye? Channel1, PlateDye? Channel2) Changes(CustomPiece piece, PlateSource source, int dyeCount, Func<byte, bool> isDye,
                                                                   Func<byte, IReadOnlyList<uint>> dyeItemsOf)
    {
        var (stored0, stored1) = BaseStains(source, dyeCount, isDye);
        PlateDye? Change(int channel, byte want, byte stored)
            => channel > dyeCount || want == stored ? null : new PlateDye(want, want == 0 ? [RemoverItemId] : dyeItemsOf(want));
        return (Change(1, piece.Stain0, stored0), Change(2, piece.Stain1, stored1));
    }

    /// <summary>
    /// The dyes the piece really has in the dresser, for the channels it has. A first value that is no dye (the dresser
    /// holds e.g. 128/7 or 232/4) marks an undyed piece: picked by hand, the game shows such a piece without dye.
    /// </summary>
    public static (byte Stain0, byte Stain1) BaseStains(PlateSource source, int dyeCount, Func<byte, bool> isDye)
    {
        if (!isDye(source.Stain0))
            return (0, 0);
        return (dyeCount >= 1 ? source.Stain0 : (byte)0, dyeCount >= 2 && isDye(source.Stain1) ? source.Stain1 : (byte)0);
    }
}

public static class PlateSlots
{
    /// <summary>Plate index → slot, the same in the plate editor and the saved plates (measured in the spike).</summary>
    public static IReadOnlyList<EquipSlot> Order => CustomSetSubjects.Slots;
}

/// <param name="Notes">Per piece (or empty slot) that needs attention: its name and why.</param>
public sealed record TransferReport(int Transferred, int Total, IReadOnlyList<(string Name, string Reason)> Notes);

public static class PlateTransfer
{
    public static string NotTaken => Loc.T("vom Spiel nicht übernommen – Kommoden- bzw. Armoire-Liste im Plate-Editor öffnen und erneut übertragen");
    public static string NotCleared => Loc.T("vom Spiel nicht geleert – Kommoden- bzw. Armoire-Liste im Plate-Editor öffnen und erneut übertragen");
    public static string NotDone => Loc.T("nicht übertragen – Glamour-Plate wurde geschlossen");
    public static string DyeNotSet => Loc.T("Farbe vom Spiel nicht vorgemerkt – im Plate-Editor von Hand färben");

    /// <summary>
    /// What a transfer achieved: the transferred pieces, and a note only where something is wrong or missing (missing
    /// piece, piece, clear or dye the game did not take, slots left when the editor closed).
    /// </summary>
    /// <param name="done">Slots the editor shows as planned.</param>
    /// <param name="failed">Slots still wrong after the last try.</param>
    /// <param name="dyeFailed">Done slots whose dyes the editor did not mark for saving.</param>
    public static TransferReport Report(IReadOnlyList<PlateSlot> plan, IReadOnlyCollection<EquipSlot> done, IReadOnlyCollection<EquipSlot> failed,
                                        IReadOnlyCollection<EquipSlot> dyeFailed, bool editorClosed, Func<uint, string> nameOf, Func<EquipSlot, string> slotName)
    {
        var notes = new List<(string Name, string Reason)>();
        foreach (var slot in plan)
        {
            var name = slot.Piece is { } piece && slot.Source != null ? nameOf(piece.ItemId) : slotName(slot.Slot);
            if (slot.Missing != null)
                notes.Add((nameOf(slot.Piece!.Value.ItemId), slot.Missing));
            if (failed.Contains(slot.Slot))
                notes.Add((name, slot.Source != null ? NotTaken : NotCleared));
            else if (!done.Contains(slot.Slot) && editorClosed)
                notes.Add((name, NotDone));
            else if (done.Contains(slot.Slot) && dyeFailed.Contains(slot.Slot))
                notes.Add((name, DyeNotSet));
        }
        return new TransferReport(plan.Count(s => s.Source != null && done.Contains(s.Slot)), plan.Count(s => s.Piece != null), notes);
    }

    /// <summary>One entry per plate slot: its source (dresser before armoire), else why it is missing; empty set slots clear the plate's slot.</summary>
    /// <param name="placesOf">Where the player keeps an item (container names, best first), for the reason.</param>
    public static IReadOnlyList<PlateSlot> Plan(IReadOnlyList<CustomPiece> pieces, Func<uint, PlateSource?> dresserOf, Func<uint, PlateSource?> armoireOf,
                                                Func<uint, IReadOnlyList<string>> placesOf)
    {
        var result = new List<PlateSlot>();
        foreach (var slot in PlateSlots.Order)
        {
            if (pieces.FirstOrDefault(p => p.Slot == slot) is not { ItemId: not 0 } piece)
            {
                result.Add(new PlateSlot(slot, null, null, null));
                continue;
            }

            if ((dresserOf(piece.ItemId) ?? armoireOf(piece.ItemId)) is not { } source)
            {
                var places = placesOf(piece.ItemId);
                result.Add(new PlateSlot(slot, piece, null, places.Count > 0 ? Loc.F("liegt in: {0}", places[0]) : Loc.T("nicht im Besitz")));
                continue;
            }
            result.Add(new PlateSlot(slot, piece, source, null));
        }
        return result;
    }

    /// <summary>
    /// A plate's 12 slots (item ids and dyes in <see cref="PlateSlots.Order"/>) as pieces; HQ ids normalized, empty
    /// slots and items the catalog does not know skipped.
    /// </summary>
    /// <param name="isKnown">Whether the catalog knows the (normalized) item.</param>
    public static IReadOnlyList<CustomPiece> Read(ReadOnlySpan<uint> itemIds, ReadOnlySpan<byte> stain0, ReadOnlySpan<byte> stain1, Func<uint, bool> isKnown)
    {
        var result = new List<CustomPiece>();
        for (var i = 0; i < PlateSlots.Order.Count && i < itemIds.Length; i++)
        {
            var raw = itemIds[i];
            var id = raw > DresserContents.HqOffset ? raw - DresserContents.HqOffset : raw;
            if (id != 0 && isKnown(id))
                result.Add(new CustomPiece(PlateSlots.Order[i], id, stain0[i], stain1[i]));
        }
        return result;
    }
}

/// <summary>Finding a piece in the glamour dresser or the armoire, the way the plate editor needs it.</summary>
public static class PlateSources
{
    /// <summary>
    /// Looks an item up in the dresser: the item itself first, then a "+N" variant of it, then the item as a piece of a
    /// stored outfit; among equals the first index. HQ entries keep their raw id.
    /// </summary>
    /// <param name="rawIds">The dresser's item ids as the game holds them (HQ above <see cref="DresserContents.HqOffset"/>).</param>
    /// <param name="outfitSets">Outfit item → its pieces (MirageStoreSetItem).</param>
    /// <param name="variants">"+N" variant → base item.</param>
    public static Func<uint, PlateSource?> Dresser(IReadOnlyList<uint> rawIds, IReadOnlyList<byte> stain0, IReadOnlyList<byte> stain1,
                                                   IReadOnlyDictionary<uint, IReadOnlyList<uint>> outfitSets, IReadOnlyDictionary<uint, uint> variants)
    {
        var best = new Dictionary<uint, (int Rank, PlateSource Source)>();
        void Offer(uint baseId, int rank, PlateSource source)
        {
            if (!best.TryGetValue(baseId, out var known) || rank < known.Rank)
                best[baseId] = (rank, source);
        }

        for (var i = 0; i < rawIds.Count; i++)
        {
            var raw = rawIds[i];
            if (raw == 0)
                continue;
            var id = raw > DresserContents.HqOffset ? raw - DresserContents.HqOffset : raw;
            var isVariant = variants.TryGetValue(id, out var baseId);
            Offer(isVariant ? baseId : id, isVariant ? 1 : 0, new PlateSource(PlateSourceKind.Dresser, (uint)i, raw, stain0[i], stain1[i]));
            if (outfitSets.TryGetValue(id, out var contained))
            {
                foreach (var piece in contained)
                    Offer(variants.GetValueOrDefault(piece, piece), 2, new PlateSource(PlateSourceKind.Dresser, (uint)i, piece, stain0[i], stain1[i]));
            }
        }
        return itemId => best.TryGetValue(itemId, out var found) ? found.Source : null;
    }

    /// <summary>Looks an item up in the armoire (rows of the Cabinet sheet that hold it): the item itself before a variant. Armoire pieces carry no dye.</summary>
    public static Func<uint, PlateSource?> Armoire(IEnumerable<(uint Row, uint ItemId)> rows, IReadOnlyDictionary<uint, uint> variants)
    {
        var best = new Dictionary<uint, (int Rank, PlateSource Source)>();
        foreach (var (row, itemId) in rows)
        {
            var isVariant = variants.TryGetValue(itemId, out var baseId);
            var key = isVariant ? baseId : itemId;
            var rank = isVariant ? 1 : 0;
            if (!best.TryGetValue(key, out var known) || rank < known.Rank)
                best[key] = (rank, new PlateSource(PlateSourceKind.Armoire, row, itemId, 0, 0));
        }
        return itemId => best.TryGetValue(itemId, out var found) ? found.Source : null;
    }
}
