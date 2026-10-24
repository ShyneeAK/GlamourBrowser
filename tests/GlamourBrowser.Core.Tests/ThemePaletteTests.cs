using GlamourBrowser.Core.Ui;

namespace GlamourBrowser.Core.Tests;

public class ThemePaletteTests
{
    private static readonly AccentColor[] Accents = Enum.GetValues<AccentColor>();

    [Fact]
    public void EveryAccentIsDistinct()
        => Assert.Equal(Accents.Length, Accents.Select(a => ThemePalette.For(a).Accent).Distinct().Count());

    [Fact]
    public void HoverIsLighterMutedIsDarker()
    {
        foreach (var accent in Accents)
        {
            var c = ThemePalette.For(accent);
            Assert.True(ThemePalette.Luminance(c.AccentHover) > ThemePalette.Luminance(c.Accent), accent.ToString());
            Assert.True(ThemePalette.Luminance(c.Accent) > ThemePalette.Luminance(c.AccentMuted), accent.ToString());
        }
    }

    [Fact]
    public void AllColorsAreOpaque()
    {
        foreach (var accent in Accents)
        {
            var c = ThemePalette.For(accent);
            uint[] all = [c.WindowBg, c.Card, c.CardRaised, c.Border, c.Text, c.TextMuted, c.Accent, c.AccentHover, c.AccentMuted, c.Owned, c.Missing, c.Warning];
            Assert.All(all, v => Assert.Equal(0xFFu, v >> 24));
        }
    }

    [Fact]
    public void OwnedStaysGreenForEveryAccent()
        => Assert.All(Accents, a => Assert.Equal(ThemePalette.For(AccentColor.Blue).Owned, ThemePalette.For(a).Owned));

    [Fact]
    public void Mix_Endpoints()
    {
        Assert.Equal(0xFF102030u, ThemePalette.Mix(0xFF102030, 0xFFF0E0D0, 0f));
        Assert.Equal(0xFFF0E0D0u, ThemePalette.Mix(0xFF102030, 0xFFF0E0D0, 1f));
    }
}
