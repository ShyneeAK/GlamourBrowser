using GlamourBrowser.Core.Localization;

namespace GlamourBrowser.Core.Tests;

public class LocTests
{
    private const string Sample = "de,en,fr,ja\n\"Waffe ziehen\",Draw weapon,,武器を抜く\n\"{0} Treffer\",\"{0} results, all\",{0} résultats,{0}件\n";

    [Fact]
    public void Csv_QuotedFieldsKeepCommasQuotesAndLineBreaks()
    {
        var rows = Csv.Parse("a,\"b, \"\"c\"\"\nd\"\r\n\ne,f");
        Assert.Equal([["a", "b, \"c\"\nd"], ["e", "f"]], rows);
    }

    [Fact]
    public void Csv_FieldQuotesOnlyWhenNeeded()
    {
        Assert.Equal("plain", Csv.Field("plain"));
        Assert.Equal("\"a, \"\"b\"\"\"", Csv.Field("a, \"b\""));
        Assert.Equal([["plain", "a, \"b\""]], Csv.Parse(Csv.Field("plain") + "," + Csv.Field("a, \"b\"")));
    }

    [Fact]
    public void Read_TakesTheLanguageColumnKeyedByGerman()
    {
        var (language, table) = Loc.Read("ja", Sample);
        Assert.Equal("ja", language);
        Assert.Equal("武器を抜く", table["Waffe ziehen"]);
        Assert.Equal("{0}件", table["{0} Treffer"]);
    }

    [Fact]
    public void Read_LeavesEmptyCellsOut()
        => Assert.False(Loc.Read("fr", Sample).Table.ContainsKey("Waffe ziehen"));

    [Fact]
    public void Read_UnknownLanguageFallsBackToEnglish()
    {
        var (language, table) = Loc.Read("ko", Sample);
        Assert.Equal("en", language);
        Assert.Equal("{0} results, all", table["{0} Treffer"]);
    }

    [Fact]
    public void Strings_EveryRowHasAllLanguagesAndNoKeyTwice()
    {
        var rows = Csv.Parse(Loc.EmbeddedCsv());
        Assert.Equal(["de", "en", "fr", "ja"], rows[0]);
        foreach (var row in rows.Skip(1))
            Assert.True(row.Count == 4 && row.All(cell => cell.Length > 0), $"incomplete row: {string.Join(" | ", row)}");
        var duplicates = rows.Skip(1).GroupBy(r => r[0]).Where(g => g.Count() > 1).Select(g => g.Key).ToList();
        Assert.Empty(duplicates);
    }

    [Fact]
    public void Strings_PlaceholdersMatchTheGermanText()
    {
        foreach (var row in Csv.Parse(Loc.EmbeddedCsv()).Skip(1))
        {
            var expected = Placeholders(row[0]);
            foreach (var cell in row.Skip(1))
                Assert.True(expected.SetEquals(Placeholders(cell)), $"placeholders differ: {row[0]} → {cell}");
        }
    }

    private static HashSet<string> Placeholders(string text)
        => System.Text.RegularExpressions.Regex.Matches(text, @"\{\d+[^}]*\}").Select(m => m.Value).ToHashSet();
}
