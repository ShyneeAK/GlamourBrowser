namespace GlamourBrowser.Core.Ui;

/// <summary>Which of the two dye boxes can be picked for an item; the others are drawn crossed out.</summary>
public static class DyeBoxes
{
    public static (bool First, bool Second) For(int dyeCount) => (dyeCount >= 1, dyeCount >= 2);
}
