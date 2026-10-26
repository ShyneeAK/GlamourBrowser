using System;
using System.Collections.Generic;
using System.IO;
using System.Numerics;
using System.Text.Json;
using GlamourBrowser.Core.Items;
using GlamourBrowser.Core.Rendering;

namespace GlamourBrowser.Rendering;

/// <summary>
/// The body measures (see <see cref="AutoFraming"/>) per character, for the face and body and idle pose they were taken
/// in; kept in ConfigDirectory/framing so that the tiles have their framing right away after a restart. Another face,
/// body or pose waits for a new measure from the live view (the slots fall back to the game's default view meanwhile).
/// </summary>
public sealed class AutoFramingService
{
    private static readonly JsonSerializerOptions Options = new() { WriteIndented = true };

    private readonly string directory = Path.Combine(Plugin.PluginInterface.ConfigDirectory.FullName, "framing");
    private readonly Dictionary<ulong, Stored?> loaded = new();

    /// <summary>A character's measures changed: the tiles render again with the new framing.</summary>
    public event Action? Changed;

    /// <summary>The automatic camera for the slot; null until the character was measured in this face, body and pose.</summary>
    public CameraPreset? For(ulong contentId, EquipSlot slot, ulong customize, byte pose, WristSide wrist = WristSide.Both)
        => Current(contentId, customize, pose) is { } body ? AutoFraming.For(slot, body, wrist) : null;

    public bool Needs(ulong contentId, ulong customize, byte pose) => contentId != 0 && Current(contentId, customize, pose) == null;

    /// <summary>A measure from the live view; kept (and the tiles told) when it is new or moved by a centimetre or more.</summary>
    public void Offer(ulong contentId, ulong customize, byte pose, BodyMeasures body)
    {
        if (contentId == 0 || Current(contentId, customize, pose) is { } known && known.CloseTo(body))
            return;
        var stored = new Stored(customize, pose, [.. Floats(body.Neck), .. Floats(body.Head), .. Floats(body.Waist), .. Floats(body.HandLeft), .. Floats(body.HandRight)]);
        loaded[contentId] = stored;
        try
        {
            Directory.CreateDirectory(directory);
            File.WriteAllText(PathFor(contentId), JsonSerializer.Serialize(stored, Options));
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            Plugin.Log.Warning(e, "Could not save the body measures");
        }
        Plugin.Log.Information($"[framing] measured: head {body.HeadSize:0.###}, top {body.Top:0.###}");
        Changed?.Invoke();
    }

    private BodyMeasures? Current(ulong contentId, ulong customize, byte pose)
    {
        if (contentId == 0)
            return null;
        if (!loaded.TryGetValue(contentId, out var stored))
            loaded[contentId] = stored = Load(contentId);
        if (stored is not { Bones.Length: 15 } || stored.Customize != customize || stored.Pose != pose)
            return null;
        var b = stored.Bones;
        return new BodyMeasures(new Vector3(b[0], b[1], b[2]), new Vector3(b[3], b[4], b[5]), new Vector3(b[6], b[7], b[8]),
                                new Vector3(b[9], b[10], b[11]), new Vector3(b[12], b[13], b[14]));
    }

    private Stored? Load(ulong contentId)
    {
        try
        {
            return File.Exists(PathFor(contentId)) ? JsonSerializer.Deserialize<Stored>(File.ReadAllText(PathFor(contentId)), Options) : null;
        }
        catch (Exception e) when (e is IOException or JsonException or UnauthorizedAccessException)
        {
            // Measured again from the live view.
            return null;
        }
    }

    private string PathFor(ulong contentId) => Path.Combine(directory, $"{contentId}.json");

    private static float[] Floats(Vector3 v) => [v.X, v.Y, v.Z];

    /// <param name="Bones">Neck, head, waist, left and right hand, three floats each.</param>
    private sealed record Stored(ulong Customize, byte Pose, float[] Bones);
}
