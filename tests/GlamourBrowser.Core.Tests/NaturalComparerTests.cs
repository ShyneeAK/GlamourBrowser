using System.Globalization;
using GlamourBrowser.Core.Sources;

namespace GlamourBrowser.Core.Tests;

public class NaturalComparerTests
{
    private static readonly NaturalComparer Comparer = new(StringComparer.Create(CultureInfo.GetCultureInfo("de-DE"), ignoreCase: true));

    private static string[] Sorted(params string[] names) => names.Order(Comparer).ToArray();

    [Fact]
    public void NumbersCountByValue()
        => Assert.Equal(["Set 1", "Set 2", "Set 3", "Set 13", "Set 14", "Set 115"], Sorted("Set 13", "Set 3", "Set 115", "Set 1", "Set 14", "Set 2"));

    [Fact]
    public void TextBetweenInTheAlphabet_IgnoringCase_ÄWithA()
        => Assert.Equal(["anzug 2", "Ärmel 1", "Robe", "robe 10", "Zylinder"], Sorted("Zylinder", "robe 10", "Ärmel 1", "Robe", "anzug 2"));

    [Fact]
    public void SeveralNumbers_AndNumbersOfAnyLength()
    {
        Assert.Equal(["Plate 2 · 3 Teile", "Plate 2 · 11 Teile", "Plate 10 · 1 Teil"], Sorted("Plate 10 · 1 Teil", "Plate 2 · 11 Teile", "Plate 2 · 3 Teile"));
        Assert.True(Comparer.Compare("Set 99999999999999999999", "Set 100000000000000000000") < 0);
    }

    [Fact]
    public void LeadingZeros_SameValue_StillAnOrder()
    {
        Assert.True(Comparer.Compare("Set 02", "Set 3") < 0);
        Assert.NotEqual(0, Comparer.Compare("Set 02", "Set 2"));
        Assert.Equal(0, Comparer.Compare("Set 2", "set 2"));
    }
}
