using GlamourBrowser.Core.Items;

namespace GlamourBrowser.Core.Tests;

public class SearchQueryTests
{
    [Fact]
    public void Parse_SplitsExcludedWordsFromTheSearchedText()
    {
        var query = SearchQuery.Parse("  Robe  der -rock  -Kleid ");

        Assert.Equal("Robe der", query.Include);
        Assert.Equal(["rock", "Kleid"], query.Excludes);
    }

    [Fact]
    public void Matches_ExcludesNamesWithAnExcludedWord()
    {
        var query = SearchQuery.Parse("-rock");

        Assert.True(query.Matches("Hose des Wanderers"));
        Assert.False(query.Matches("Faltenrock"));
        Assert.False(query.Matches("Rock der Weisen"));
    }

    [Fact]
    public void Matches_NeedsTheSearchedTextAndNoExcludedWord()
    {
        var query = SearchQuery.Parse("weisen -rock");

        Assert.True(query.Matches("Robe der Weisen"));
        Assert.False(query.Matches("Rock der Weisen"));
        Assert.False(query.Matches("Faltenrock"));
    }

    [Fact]
    public void Matches_ALoneMinusExcludesNothing()
    {
        var query = SearchQuery.Parse("-");

        Assert.True(query.IsEmpty);
        Assert.True(query.Matches("Rock"));
    }

    [Fact]
    public void Matches_ASetByItsNameOrAPiece_ButNoneMayHaveAnExcludedWord()
    {
        Assert.True(SearchQuery.Parse("weisen").Matches(["Set der Gelehrten", "Robe der Weisen"]));
        Assert.False(SearchQuery.Parse("-rock").Matches(["Set der Gelehrten", "Faltenrock"]));
    }
}
