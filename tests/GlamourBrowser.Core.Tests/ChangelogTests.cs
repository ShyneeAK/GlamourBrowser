using GlamourBrowser.Core.Ui;

namespace GlamourBrowser.Core.Tests;

public class ChangelogTests
{
    private const string Sample = "# Changelog\r\n\r\n## 0.4.0.0\r\n- Neu: A\r\n  - eingerückt\r\n\r\n## 0.3.1.0\nText\n## 0.3.0.0\n- Alt\n";

    private static IReadOnlyList<ChangelogEntry> Entries => Changelog.Parse(Sample);

    [Fact]
    public void Parse_VersionsNewestFirstWithTheirLines()
    {
        Assert.Equal([new Version(0, 4, 0, 0), new Version(0, 3, 1, 0), new Version(0, 3, 0, 0)], Entries.Select(e => e.Version));
        Assert.Equal(["- Neu: A", "- eingerückt"], Entries[0].Lines);
        Assert.Equal(["Text"], Entries[1].Lines);
    }

    [Fact]
    public void Since_TheVersionsNotSeenYet()
        => Assert.Equal([new Version(0, 4, 0, 0), new Version(0, 3, 1, 0)],
            Changelog.Since(Entries, new Version(0, 3, 0, 0), new Version(0, 4, 0, 0)).Select(e => e.Version));

    [Fact]
    public void Since_WithoutLastSeen_EveryEntryUpToTheCurrent()
        => Assert.Equal([new Version(0, 3, 1, 0), new Version(0, 3, 0, 0)], Changelog.Since(Entries, null, new Version(0, 3, 1, 0)).Select(e => e.Version));

    [Fact]
    public void Since_LeavesOutVersionsAfterTheCurrent()
        => Assert.Empty(Changelog.Since(Entries, new Version(0, 3, 0, 0), new Version(0, 3, 0, 0)));

    [Theory]
    [InlineData("de")]
    [InlineData("en")]
    [InlineData("fr")]
    [InlineData("ja")]
    public void Embedded_EveryLanguageHasTheSameVersions(string language)
    {
        var german = Changelog.Parse(Changelog.Embedded("de")).Select(e => e.Version);
        var entries = Changelog.Parse(Changelog.Embedded(language));
        Assert.Equal(german, entries.Select(e => e.Version));
        Assert.All(entries, e => Assert.NotEmpty(e.Lines));
    }
}
