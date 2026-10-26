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

    // Turned three quarters for a head, from the side for hands, bracelets and rings: the right hand faces the camera,
    // the left one for a bracelet on the left arm only and for the left ring.
    private const float HeadTurn = -0.655f;
    private const float HandTurn = 1.055f;

    /// <param name="wrist">For a bracelet on the left arm only: the mirror image of the hand's camera, the left hand facing it.</param>
    /// <param name="zoom">The player's zoom of the automatic camera: 2 shows half the height, around the same middle.</param>
    public static CameraPreset For(EquipSlot slot, BodyMeasures body, WristSide wrist = WristSide.Both, float zoom = 1f)
    {
        var head = body.HeadSize;
        var preset = slot switch
        {
            EquipSlot.Head or EquipSlot.Ears => Frame(body.Head + new Vector3(0, 0.26f * head, 0), 8.1f * head, HeadTurn),
            EquipSlot.Neck => Frame(body.Neck - new Vector3(0, 0.5f * head, 0), 8.1f * head, 0),
            // The left ring and a bracelet on the left arm only: the mirror image, so the tiles look alike while scrolling.
            EquipSlot.RingLeft => Mirrored(body, Hand(body)),
            EquipSlot.Wrists when wrist == WristSide.Left => Mirrored(body, Hand(body)),
            EquipSlot.Hands or EquipSlot.Wrists or EquipSlot.RingRight => Hand(body),
            EquipSlot.Feet => Frame(new Vector3(body.Waist.X, 0.25f * body.Top, body.Waist.Z), 0.92f * body.Top, 0, keepX: true),
            // Body, legs and weapons: the whole character, like the game's view of a Miqo'te of middle height.
            _ => Frame(new Vector3(0, 0.56f * body.Top, 0), 1.2f * body.Top, 0, keepX: true),
        };
        return preset with { Distance = preset.Distance / Math.Clamp(zoom, 0.25f, 4f) };
    }

    /// <summary>
    /// The right hand alone, from the side, in the middle of the view: wherever the pose holds it (in front of the body
    /// or behind it), the same picture on every character.
    /// </summary>
    private static CameraPreset Hand(BodyMeasures body) => Frame(body.HandRight, 8.1f * body.HeadSize, HandTurn);

    /// <summary>
    /// The mirror image of the hand's camera for the left hand: turned the other way, the left hand where
    /// the right one is in <paramref name="right"/> but on the other side of the middle, at the same height and zoom; so
    /// the tiles look alike while scrolling.
    /// </summary>
    private static CameraPreset Mirrored(BodyMeasures body, CameraPreset right)
    {
        var rightHand = Turn(body.HandRight, right.Rotation) + new Vector3(right.PositionX, right.PositionY, 0);
        var leftHand = Turn(body.HandLeft, -right.Rotation);
        return new CameraPreset(-right.Rotation, -rightHand.X - leftHand.X, rightHand.Y - leftHand.Y, right.Distance);
    }

    private static Vector3 Turn(Vector3 point, float turn) => Vector3.Transform(point, Quaternion.CreateFromAxisAngle(Vector3.UnitY, turn));

    /// <summary>The preset that shows <paramref name="centre"/> (model space) in the middle of a view <paramref name="height"/> high.</summary>
    /// <param name="keepX">Not shifted sideways (the whole body stands in the middle as it is).</param>
    private static CameraPreset Frame(Vector3 centre, float height, float turn, bool keepX = false)
    {
        var turned = Turn(centre, turn);
        return new CameraPreset(turn, keepX ? 0 : -turned.X, ViewCentreY - turned.Y, height);
    }
}
