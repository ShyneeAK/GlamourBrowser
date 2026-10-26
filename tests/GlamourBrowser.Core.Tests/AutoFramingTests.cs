using System.Numerics;
using GlamourBrowser.Core.Items;
using GlamourBrowser.Core.Rendering;

namespace GlamourBrowser.Core.Tests;

public class AutoFramingTests
{
    // Measured in the live view: a Miqo'te of middle height in her idle pose.
    private static readonly BodyMeasures Miqote = new(
        Neck: new Vector3(0.009f, 1.312f, 0.039f), Head: new Vector3(0.019f, 1.4f, 0.081f), Waist: new Vector3(0.032f, 0.968f, 0.085f),
        HandLeft: new Vector3(0.25f, 0.847f, 0.006f), HandRight: new Vector3(-0.189f, 0.827f, 0.12f));

    // Her hand calibrations and the game's default view, to which the proportions were fitted.
    [Theory]
    [InlineData(EquipSlot.Head, -0.655f, -0.575f, 0.714f)]
    [InlineData(EquipSlot.Hands, 1.055f, 0f, 0.714f)]
    [InlineData(EquipSlot.Feet, 0f, 0.467f, 1.392f)]
    [InlineData(EquipSlot.Body, 0f, 0f, 1.818f)]
    public void For_MatchesTheHandCalibrations(EquipSlot slot, float rotation, float positionY, float distance)
    {
        var preset = AutoFraming.For(slot, Miqote);

        Assert.Equal(rotation, preset.Rotation, 0.001f);
        Assert.Equal(positionY, preset.PositionY, 0.015f);
        Assert.Equal(distance, preset.Distance, 0.01f);
    }

    [Fact]
    public void For_ScalesWithTheCharacter()
    {
        // Half the size: a Lalafell-like body in the same proportions shows as large in the tile.
        var small = new BodyMeasures(Miqote.Neck / 2, Miqote.Head / 2, Miqote.Waist / 2, Miqote.HandLeft / 2, Miqote.HandRight / 2);

        Assert.Equal(AutoFraming.For(EquipSlot.Head, Miqote).Distance / 2, AutoFraming.For(EquipSlot.Head, small).Distance, 0.001f);
        Assert.Equal(AutoFraming.For(EquipSlot.Legs, Miqote).Distance / 2, AutoFraming.For(EquipSlot.Legs, small).Distance, 0.001f);
    }

    [Fact]
    public void CloseTo_ACentimetreApartIsNew()
    {
        Assert.True(Miqote.CloseTo(Miqote with { Head = Miqote.Head + new Vector3(0, 0.005f, 0) }));
        Assert.False(Miqote.CloseTo(Miqote with { Head = Miqote.Head + new Vector3(0, 0.02f, 0) }));
    }

    [Fact]
    public void For_ABraceletOnOneArmFacesThatHand()
    {
        var right = AutoFraming.For(EquipSlot.Wrists, Miqote, WristSide.Right);
        var left = AutoFraming.For(EquipSlot.Wrists, Miqote, WristSide.Left);

        Assert.True(right.Rotation > 0);
        Assert.Equal(-right.Rotation, left.Rotation);
        Assert.Equal(AutoFraming.ViewCentreY - Miqote.HandLeft.Y, left.PositionY, 0.001f);
        Assert.Equal(AutoFraming.For(EquipSlot.Hands, Miqote), AutoFraming.For(EquipSlot.Wrists, Miqote, WristSide.Both));
    }

    [Theory]
    [InlineData("j_kosi\0j_te_r\0j_ude_b_r\0", WristSide.Right)]
    [InlineData("j_kosi\0n_hte_l\0", WristSide.Left)]
    [InlineData("j_te_r\0j_te_l\0", WristSide.Both)]
    [InlineData("j_kosi\0", WristSide.Both)]
    [InlineData("j_te_rx\0", WristSide.Both)]
    public void SideOf_TheArmsTheModelIsSkinnedTo(string strings, WristSide side)
        => Assert.Equal(side, WristModels.SideOf(System.Text.Encoding.ASCII.GetBytes("MDL" + strings.Replace("\\0", "\0"))));
}
