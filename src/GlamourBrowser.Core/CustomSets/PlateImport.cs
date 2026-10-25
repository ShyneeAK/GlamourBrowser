namespace GlamourBrowser.Core.CustomSets;

/// <summary>Importing the saved glamour plates as custom sets, without duplicates.</summary>
public static class PlateImport
{
    /// <summary>
    /// The plates worth importing: not empty, and not the same pieces and dyes as an existing set or an earlier plate.
    /// Variants are compared (and returned) as their base item.
    /// </summary>
    /// <returns>The plate's index (0-based) and its pieces.</returns>
    public static IReadOnlyList<(int Plate, IReadOnlyList<CustomPiece> Pieces)> NewSets(IReadOnlyList<IReadOnlyList<CustomPiece>> plates,
                                                                                      IEnumerable<CustomSet> existing, IReadOnlyDictionary<uint, uint> variants)
    {
        IReadOnlyList<CustomPiece> Normalize(IEnumerable<CustomPiece> pieces)
            => pieces.Where(p => p.ItemId != 0).Select(p => p with { ItemId = variants.GetValueOrDefault(p.ItemId, p.ItemId) }).OrderBy(p => p.Slot).ToList();
        static string Key(IReadOnlyList<CustomPiece> pieces) => string.Join("|", pieces);

        var known = existing.Select(s => Key(Normalize(s.Pieces))).ToHashSet();
        var result = new List<(int, IReadOnlyList<CustomPiece>)>();
        for (var i = 0; i < plates.Count; i++)
        {
            var pieces = Normalize(plates[i]);
            if (pieces.Count > 0 && known.Add(Key(pieces)))
                result.Add((i, pieces));
        }
        return result;
    }
}
