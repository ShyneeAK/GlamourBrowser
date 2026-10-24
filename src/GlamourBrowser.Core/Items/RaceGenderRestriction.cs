namespace GlamourBrowser.Core.Items;

/// <summary>Who may wear an item. Race 1 Hyur … 8 Viera; sex 0 male, 1 female.</summary>
public sealed record RaceGenderRestriction(IReadOnlySet<byte> Races, bool Male, bool Female)
{
    public static RaceGenderRestriction None { get; } = new(new HashSet<byte> { 1, 2, 3, 4, 5, 6, 7, 8 }, true, true);

    public bool Allows(byte race, byte sex) => Races.Contains(race) && (sex == 0 ? Male : Female);
}
