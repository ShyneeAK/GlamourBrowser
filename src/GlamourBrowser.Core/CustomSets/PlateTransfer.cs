using GlamourBrowser.Core.Items;
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
/// <param name="Stain0">The dye the piece is stored with; the plate keeps it (dyes set from outside are not saved).</param>
public readonly record struct PlateSource(PlateSourceKind Kind, uint Id, uint ItemId, byte Stain0, byte Stain1);

/// <param name="Piece">The set's piece for this slot; null clears the slot.</param>
/// <param name="Missing">Why a piece cannot be transferred ("liegt in: …", "nicht im Besitz").</param>
/// <param name="DyeNote">The set's dye differs from the stored one: "im Spiel färben: …".</param>
public sealed record PlateSlot(EquipSlot Slot, CustomPiece? Piece, PlateSource? Source, string? Missing, string? DyeNote);

public static class PlateSlots
{
    /// <summary>Plate index → slot, the same in the plate editor and the saved plates (measured in the spike).</summary>
    public static IReadOnlyList<EquipSlot> Order => CustomSetSubjects.Slots;
}

/// <param name="Notes">Per piece (or empty slot) that needs attention: its name and why.</param>
public sealed record TransferReport(int Transferred, int Total, IReadOnlyList<(string Name, string Reason)> Notes);

public static class PlateTransfer
{
    public const string NotTaken = "vom Spiel nicht übernommen – Kommoden- bzw. Armoire-Liste im Plate-Editor öffnen und erneut übertragen";
    public const string NotCleared = "vom Spiel nicht geleert – Kommoden- bzw. Armoire-Liste im Plate-Editor öffnen und erneut übertragen";
    public const string NotDone = "nicht übertragen – Glamour-Plate wurde geschlossen";

    /// <summary>
    /// What a transfer achieved: the transferred pieces, and a note for every slot that is not as planned (missing
    /// piece, piece or clear the game did not take, slots left when the editor closed) and for different dyes.
    /// </summary>
    /// <param name="done">Slots the editor shows as planned.</param>
    /// <param name="failed">Slots still wrong after the last try.</param>
    public static TransferReport Report(IReadOnlyList<PlateSlot> plan, IReadOnlyCollection<EquipSlot> done, IReadOnlyCollection<EquipSlot> failed,
                                        bool editorClosed, Func<uint, string> nameOf, Func<EquipSlot, string> slotName)
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
            else if (slot.DyeNote != null && done.Contains(slot.Slot))
                notes.Add((name, slot.DyeNote));
        }
        return new TransferReport(plan.Count(s => s.Source != null && done.Contains(s.Slot)), plan.Count(s => s.Piece != null), notes);
    }

    /// <summary>One entry per plate slot: its source (dresser before armoire), else why it is missing; empty set slots clear the plate's slot.</summary>
    /// <param name="placesOf">Where the player keeps an item (container names, best first), for the reason.</param>
    public static IReadOnlyList<PlateSlot> Plan(IReadOnlyList<CustomPiece> pieces, Func<uint, PlateSource?> dresserOf, Func<uint, PlateSource?> armoireOf,
                                                Func<uint, IReadOnlyList<string>> placesOf, Func<byte, string> dyeName)
    {
        var result = new List<PlateSlot>();
        foreach (var slot in PlateSlots.Order)
        {
            if (pieces.FirstOrDefault(p => p.Slot == slot) is not { ItemId: not 0 } piece)
            {
                result.Add(new PlateSlot(slot, null, null, null, null));
                continue;
            }

            if ((dresserOf(piece.ItemId) ?? armoireOf(piece.ItemId)) is not { } source)
            {
                var places = placesOf(piece.ItemId);
                result.Add(new PlateSlot(slot, piece, null, places.Count > 0 ? $"liegt in: {places[0]}" : "nicht im Besitz", null));
                continue;
            }

            string? dyeNote = null;
            if (piece.Stain0 != source.Stain0 || piece.Stain1 != source.Stain1)
                dyeNote = "im Spiel färben: " + dyeName(piece.Stain0) + (piece.Stain1 != 0 || source.Stain1 != 0 ? " / " + dyeName(piece.Stain1) : "");
            result.Add(new PlateSlot(slot, piece, source, null, dyeNote));
        }
        return result;
    }

    /// <summary>A plate's 12 slots (item ids and dyes in <see cref="PlateSlots.Order"/>) as pieces; HQ ids normalized, empty slots skipped.</summary>
    public static IReadOnlyList<CustomPiece> Read(ReadOnlySpan<uint> itemIds, ReadOnlySpan<byte> stain0, ReadOnlySpan<byte> stain1)
    {
        var result = new List<CustomPiece>();
        for (var i = 0; i < PlateSlots.Order.Count && i < itemIds.Length; i++)
        {
            var raw = itemIds[i];
            if (raw != 0)
                result.Add(new CustomPiece(PlateSlots.Order[i], raw > DresserContents.HqOffset ? raw - DresserContents.HqOffset : raw, stain0[i], stain1[i]));
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
