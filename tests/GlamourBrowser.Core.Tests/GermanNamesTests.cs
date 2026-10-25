using GlamourBrowser.Core.Sources;

namespace GlamourBrowser.Core.Tests;

public class GermanNamesTests
{
    // ENpcResident.Pronoun: 0 masculine, 1 feminine, 2 neuter.
    [Theory]
    [InlineData("niedergeschlagen[a] Soldat", 0, "Niedergeschlagener Soldat")]
    [InlineData("fahrend[a] Händlerin", 1, "Fahrende Händlerin")]
    [InlineData("wartend[a] Mädchen", 2, "Wartendes Mädchen")]
    [InlineData("Highwind-Bedienstet[a]", 1, "Highwind-Bedienstete")]
    [InlineData("Soldat[p] von Nophicas Schar", 0, "Soldat von Nophicas Schar")]
    [InlineData("Gyges [t] Groß[a]", 0, "Gyges der Große")]
    [InlineData("Stilles Moos [t] Bedächtig[a]", 1, "Stilles Moos die Bedächtige")]
    [InlineData("U'linbho [t] Sandteuflin[p]", 1, "U'linbho die Sandteuflin")]
    [InlineData("Varsarudh", 0, "Varsarudh")]
    [InlineData("Name[x]", 0, "Name")]
    public void Nominative(string singular, int pronoun, string expected)
        => Assert.Equal(expected, GermanNames.Nominative(singular, pronoun));
}
