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
    public void For_ABraceletOnTheLeftArmIsTheMirrorImage()
    {
        var right = AutoFraming.For(EquipSlot.Wrists, Miqote, WristSide.Right);
        var left = AutoFraming.For(EquipSlot.Wrists, Miqote, WristSide.Left);
        Vector3 OnScreen(Vector3 hand, CameraPreset camera)
            => Vector3.Transform(hand, Quaternion.CreateFromAxisAngle(Vector3.UnitY, camera.Rotation)) + new Vector3(camera.PositionX, camera.PositionY, 0);

        Assert.Equal(AutoFraming.For(EquipSlot.Hands, Miqote), right);
        Assert.Equal(-right.Rotation, left.Rotation);
        Assert.Equal(right.Distance, left.Distance);
        Assert.Equal(-OnScreen(Miqote.HandRight, right).X, OnScreen(Miqote.HandLeft, left).X, 0.0001f);
        Assert.Equal(OnScreen(Miqote.HandRight, right).Y, OnScreen(Miqote.HandLeft, left).Y, 0.0001f);
    }

    [Theory]
    [InlineData("j_kosi\0j_te_r\0j_ude_b_r\0", WristSide.Right)]
    [InlineData("j_kosi\0n_hte_l\0", WristSide.Left)]
    [InlineData("j_te_r\0j_te_l\0", WristSide.Both)]
    [InlineData("j_kosi\0", WristSide.Both)]
    [InlineData("j_te_rx\0", WristSide.Both)]
    public void SideOf_TheArmsTheModelIsSkinnedTo(string strings, WristSide side)
        => Assert.Equal(side, WristModels.SideOf(System.Text.Encoding.ASCII.GetBytes("MDL" + strings.Replace("\\0", "\0"))));

    [Fact]
    public void For_TheLeftRingIsTheMirrorImageOfTheRight()
    {
        var right = AutoFraming.For(EquipSlot.RingRight, Miqote);
        var left = AutoFraming.For(EquipSlot.RingLeft, Miqote);
        Vector3 OnScreen(Vector3 hand, CameraPreset camera)
            => Vector3.Transform(hand, Quaternion.CreateFromAxisAngle(Vector3.UnitY, camera.Rotation)) + new Vector3(camera.PositionX, camera.PositionY, 0);

        Assert.Equal(-right.Rotation, left.Rotation);
        Assert.Equal(right.Distance, left.Distance);
        Assert.Equal(-OnScreen(Miqote.HandRight, right).X, OnScreen(Miqote.HandLeft, left).X, 0.0001f);
        Assert.Equal(OnScreen(Miqote.HandRight, right).Y, OnScreen(Miqote.HandLeft, left).Y, 0.0001f);
    }

    [Fact]
    public void For_HandsFarApartStayInside()
    {
        // The arms held away from the body (an Au'Ra in his idle pose): both hands with room for the fingers.
        var wide = Miqote with { HandLeft = new Vector3(0.45f, 0.85f, 0.02f), HandRight = new Vector3(-0.42f, 0.83f, 0.1f) };
        var camera = AutoFraming.For(EquipSlot.Hands, wide);
        float OnScreenX(Vector3 hand) => Vector3.Transform(hand, Quaternion.CreateFromAxisAngle(Vector3.UnitY, camera.Rotation)).X + camera.PositionX;
        var halfWidth = camera.Distance * AutoFraming.Aspect / 2;

        Assert.True(camera.Distance > AutoFraming.For(EquipSlot.Hands, Miqote).Distance);
        Assert.InRange(OnScreenX(wide.HandLeft), -halfWidth + 0.1f, halfWidth - 0.1f);
        Assert.InRange(OnScreenX(wide.HandRight), -halfWidth + 0.1f, halfWidth - 0.1f);
    }
}
