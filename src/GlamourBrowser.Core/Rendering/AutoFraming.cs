using System.Numerics;
using GlamourBrowser.Core.Items;

namespace GlamourBrowser.Core.Rendering;

/// <summary>
/// A few bones of the character in model space (feet on the ground at y = 0, facing +z), measured once from the live
/// view: what <see cref="AutoFraming"/> frames each slot by.
/// </summary>
public sealed record BodyMeasures(Vector3 Neck, Vector3 Head, Vector3 Waist, Vector3 HandLeft, Vector3 HandRight)
{
    /// <summary>The head's size: from the neck to the head bone.</summary>
    public float HeadSize => MathF.Max(0.01f, Head.Y - Neck.Y);

    /// <summary>About the top of the head (hair, ears aside).</summary>
    public float Top => Head.Y + 1.3f * HeadSize;

    /// <summary>Two measures are the same when no bone moved by a centimetre or more: then the tiles need no new render.</summary>
    public bool CloseTo(BodyMeasures other)
        => Near(Neck, other.Neck) && Near(Head, other.Head) && Near(Waist, other.Waist) && Near(HandLeft, other.HandLeft) && Near(HandRight, other.HandRight);

    private static bool Near(Vector3 a, Vector3 b) => Vector3.Distance(a, b) < 0.01f;
}

/// <summary>
/// The camera for a slot from the character's bones, for every race and height: the CharaView camera is orthographic,
/// looking at <see cref="ViewCentreY"/>; <see cref="CameraPreset.Distance"/> is the visible height. The proportions
/// come from hand calibrations of a Miqo'te of middle height (head, hands, feet) and the game's own full-body view.
/// </summary>
public static class AutoFraming
{
    /// <summary>The height the CharaView camera looks at; the character's position shifts it there.</summary>
    public const float ViewCentreY = 0.848f;

    // Turned three quarters for a head, from the side for hands (the rings and bracelets show).
    private const float HeadTurn = -0.655f;
    private const float HandTurn = 1.055f;

    public static CameraPreset For(EquipSlot slot, BodyMeasures body)
    {
        var head = body.HeadSize;
        return slot switch
        {
            EquipSlot.Head or EquipSlot.Ears => Frame(body.Head + new Vector3(0, 0.26f * head, 0), 8.1f * head, HeadTurn),
            EquipSlot.Neck => Frame(body.Neck - new Vector3(0, 0.5f * head, 0), 8.1f * head, 0),
            EquipSlot.Hands or EquipSlot.Wrists or EquipSlot.RingRight or EquipSlot.RingLeft
                => Frame((body.HandLeft + body.HandRight) / 2, 8.1f * head, HandTurn),
            EquipSlot.Feet => Frame(new Vector3(body.Waist.X, 0.25f * body.Top, body.Waist.Z), 0.92f * body.Top, 0, keepX: true),
            // Body, legs and weapons: the whole character, like the game's view of a Miqo'te of middle height.
            _ => Frame(new Vector3(0, 0.56f * body.Top, 0), 1.2f * body.Top, 0, keepX: true),
        };
    }

    /// <summary>The preset that shows <paramref name="centre"/> (model space) in the middle of a view <paramref name="height"/> high.</summary>
    /// <param name="keepX">Not shifted sideways (the whole body stands in the middle as it is).</param>
    private static CameraPreset Frame(Vector3 centre, float height, float turn, bool keepX = false)
    {
        var turned = Vector3.Transform(centre, Quaternion.CreateFromAxisAngle(Vector3.UnitY, turn));
        return new CameraPreset(turn, keepX ? 0 : -turned.X, ViewCentreY - turned.Y, height);
    }
}
