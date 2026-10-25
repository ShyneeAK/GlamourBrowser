using GlamourBrowser.Core.Sets;

namespace GlamourBrowser.Core.Tests;

public class SetNamesTests
{
    [Theory]
    [InlineData("Asuran-Set des Spähens", "Asuran-Hachigane des Spähens", "Asuran-Togi des Spähens", "Asuran-Tekko des Spähens")]
    [InlineData("Garlond-Set der Verteidigung", "Garlond-Helm der Verteidigung", "Garlond-Körperpanzer der Verteidigung", "Garlond-Bärlatschen der Verteidigung")]
    [InlineData("Neo-Königreich-Set des Spähens", "Neo-Königreich-Chapeau des Spähens", "Neo-Königreich-Tunika des Spähens", "Neo-Königreich-Stiefel des Spähens")]
    [InlineData("Straßen-Set", "Straßen-Kappe", "Straßen-Oberteil", "Straßen-Schuhe")]
    [InlineData("Hanf-Set", "Hanf-Dalmatika †", "Hanf-Sarouel †", "Hanf-Poulaines")]
    [InlineData("Ishgard-Set", "Ishgard Hut", "Ishgard Mantel", "Ishgard Stiefel")]
    [InlineData(null, "Gordische Krone der Magie", "Gordischer Talar der Magie", "Gordische Hose der Magie")]
    [InlineData(null, "Visier des Grobians +1", "Lorica des Grobians +1", "Sollerets des Grobians +1")]
    public void FromCommonStart(string? expected, params string[] names) => Assert.Equal(expected, SetNames.FromCommonStart(names));

    [Fact]
    public void CommonStart_KeepsTheSeparator()
        => Assert.Equal("Asuran-", SetNames.CommonStart(["Asuran-Hachigane des Spähens", "Asuran-Togi des Spähens"]));

    [Fact]
    public void CommonStart_EmptyWithoutSeparator()
        => Assert.Equal("", SetNames.CommonStart(["Gordische Krone der Magie", "Gordischer Talar der Magie"]));

    [Fact]
    public void Fallback_AddsTheSuffix_WithoutDagger()
    {
        Assert.Equal("Gordischer Talar der Magie (Set)", SetNames.Fallback("Gordischer Talar der Magie"));
        Assert.Equal("Hanf-Dalmatika (Set)", SetNames.Fallback("Hanf-Dalmatika †"));
    }
}
