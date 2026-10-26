using System;
using System.Collections.Generic;
using System.Globalization;
using System.Numerics;
using FFXIVClientStructs.FFXIV.Client.Graphics.Kernel;
using FFXIVClientStructs.FFXIV.Client.Graphics.Render;
using FFXIVClientStructs.FFXIV.Client.Graphics.Scene;
using FFXIVClientStructs.FFXIV.Client.UI.Misc;
using RenderCamera = FFXIVClientStructs.FFXIV.Client.Graphics.Render.Camera;
using SceneCamera = FFXIVClientStructs.FFXIV.Client.Graphics.Scene.Camera;

namespace GlamourBrowser.Rendering;

/// <summary>
/// Diagnostics for automatic framing (/glamourdresser camera): a CharaView's camera (projection, position) and its
/// character's bones in world space, to relate bone positions to what the view shows.
/// </summary>
public static unsafe class CameraProbe
{
    public static List<string> Describe(CharaView* view, Texture* texture)
    {
        var lines = new List<string>();
        if (view == null || view->Camera == null)
        {
            lines.Add("no view");
            return lines;
        }
        var scene = (SceneCamera*)view->Camera;
        var render = scene->RenderCamera;
        lines.Add($"texture {(texture != null ? $"{texture->ActualWidth}x{texture->ActualHeight}" : "none")}, zoom ratio {F(view->ZoomRatio)}");
        lines.Add($"scene camera pos {V(scene->Position)} lookAt {V(scene->LookAtVector)} rot {Q(scene->Rotation)}");
        if (render != null)
        {
            lines.Add($"render camera ortho {render->IsOrtho} fov {F(render->FoV)} fov2 {F(render->FoV_2)} aspect {F(render->AspectRatio)} "
                      + $"orthoHeight {F(render->OrthoHeight)} near {F(render->NearPlane)} far {F(render->FarPlane)} origin {V(render->Origin)}");
            lines.Add($"render view {M(render->ViewMatrix)}");
            lines.Add($"render proj {M(render->ProjectionMatrix)}");
        }

        var character = view->GetCharacter();
        if (character == null)
        {
            lines.Add("no character");
            return lines;
        }
        lines.Add($"character pos {V(character->GameObject.Position)} rot {F(character->GameObject.Rotation)} scale {F(character->GameObject.Scale)} height {character->DrawData.CustomizeData.Height}");
        var drawObject = (CharacterBase*)character->GameObject.DrawObject;
        if (drawObject == null || drawObject->Skeleton == null)
        {
            lines.Add("no skeleton");
            return lines;
        }
        lines.Add($"draw object pos {V(drawObject->DrawObject.Object.Position)} rot {Q(drawObject->DrawObject.Object.Rotation)} scale {V(drawObject->DrawObject.Object.Scale)}");
        var skeleton = drawObject->Skeleton;
        var transform = skeleton->Transform;
        lines.Add($"skeleton pos {V(transform.Position)} rot {Q(transform.Rotation)} scale {V(transform.Scale)} partials {skeleton->PartialSkeletonCount}");
        for (var p = 0; p < skeleton->PartialSkeletonCount; p++)
        {
            var pose = skeleton->PartialSkeletons[p].GetHavokPose(0);
            if (pose == null || pose->Skeleton == null)
                continue;
            for (var b = 0; b < pose->ModelPose.Length && b < pose->Skeleton->Bones.Length; b++)
            {
                var t = pose->ModelPose[b].Translation;
                var model = new Vector3(t.X, t.Y, t.Z);
                var position = new Vector3(transform.Position.X, transform.Position.Y, transform.Position.Z);
                var scale = new Vector3(transform.Scale.X, transform.Scale.Y, transform.Scale.Z);
                var rotation = new Quaternion(transform.Rotation.X, transform.Rotation.Y, transform.Rotation.Z, transform.Rotation.W);
                var world = position + Vector3.Transform(model * scale, rotation);
                lines.Add($"bone {p}/{b} {pose->Skeleton->Bones[b].Name.String} world {V(world)}");
            }
        }
        return lines;
    }

    private static string F(float value) => value.ToString("0.###", CultureInfo.InvariantCulture);
    private static string V(Vector3 v) => $"({F(v.X)}, {F(v.Y)}, {F(v.Z)})";
    private static string Q(Quaternion q) => $"({F(q.X)}, {F(q.Y)}, {F(q.Z)}, {F(q.W)})";

    private static string M(Matrix4x4 m)
        => $"[{F(m.M11)} {F(m.M12)} {F(m.M13)} {F(m.M14)} | {F(m.M21)} {F(m.M22)} {F(m.M23)} {F(m.M24)} | "
           + $"{F(m.M31)} {F(m.M32)} {F(m.M33)} {F(m.M34)} | {F(m.M41)} {F(m.M42)} {F(m.M43)} {F(m.M44)}]";
}
