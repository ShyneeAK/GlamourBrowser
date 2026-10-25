using System;
using FFXIVClientStructs.FFXIV.Client.Game.Character;
using FFXIVClientStructs.FFXIV.Client.Graphics.Scene;
using FFXIVClientStructs.Havok.Animation.Playback.Control.Default;
using GlamourBrowser.Core.Rendering;

namespace GlamourBrowser.Rendering;

/// <summary>
/// Sets and freezes the moment of a character's animations through the Havok animation controls of its
/// skeleton (the way GPose tools do it): CharacterBase.Skeleton → PartialSkeletons[i] → animated skeleton → controls.
/// </summary>
public static unsafe class AnimationFreeze
{
    /// <summary>Puts every animation at its last frame and pauses it (each in its own length).</summary>
    /// <returns>False when the character has no animation controls (yet).</returns>
    public static bool FreezeAtEnd(Character* character)
        => ForEachControl(character, control =>
        {
            control->hkaAnimationControl.LocalTime = AnimationTiming.LastFrame(Duration(control));
            control->PlaybackSpeed = 0f;
        });

    /// <summary>Pauses or resumes every animation without moving it.</summary>
    public static bool SetPaused(Character* character, bool paused)
        => ForEachControl(character, control => control->PlaybackSpeed = paused ? 0f : 1f);

    /// <summary>
    /// Body animations (first partial skeleton) that still carry weight: more than one means a blend from the
    /// previous animation is still running.
    /// </summary>
    public static int BodyBlend(Character* character)
    {
        var active = 0;
        ForEachControl(character, control =>
        {
            if (control->hkaAnimationControl.Weight > 0.01f)
                active++;
        }, bodyOnly: true);
        return active;
    }

    private static float Duration(hkaDefaultAnimationControl* control)
    {
        var binding = control->hkaAnimationControl.Binding.ptr;
        if (binding == null || binding->Animation.ptr == null)
            return 0;
        return binding->Animation.ptr->Duration;
    }

    private static bool ForEachControl(Character* character, ControlAction action, bool bodyOnly = false)
    {
        if (character == null)
            return false;
        try
        {
            var drawObject = (CharacterBase*)character->GameObject.DrawObject;
            if (drawObject == null || drawObject->Skeleton == null)
                return false;
            var skeleton = drawObject->Skeleton;
            var any = false;
            var partials = bodyOnly ? Math.Min(1, (int)skeleton->PartialSkeletonCount) : skeleton->PartialSkeletonCount;
            for (var i = 0; i < partials; i++)
            {
                var animated = skeleton->PartialSkeletons[i].GetHavokAnimatedSkeleton(0);
                if (animated == null)
                    continue;
                for (var j = 0; j < animated->AnimationControls.Length; j++)
                {
                    var control = animated->AnimationControls[j].Value;
                    if (control == null)
                        continue;
                    action(control);
                    any = true;
                }
            }
            return any;
        }
        catch (Exception e)
        {
            Plugin.Log.Warning(e, "Animation access failed");
            return false;
        }
    }

    private delegate void ControlAction(hkaDefaultAnimationControl* control);
}
