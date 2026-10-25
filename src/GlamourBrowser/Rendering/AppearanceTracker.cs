using System;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game.Character;
using FFXIVClientStructs.FFXIV.Client.Game.Control;
using FFXIVClientStructs.FFXIV.Client.Game.UI;

namespace GlamourBrowser.Rendering;

/// <summary>Watches the local player's look (customize data + worn outfit) once per second.</summary>
public sealed unsafe class AppearanceTracker : IDisposable
{
    private DateTime nextCheck = DateTime.MinValue;

    public ulong Hash { get; private set; }
    /// <summary>The look without the outfit (face, body): tiles take their outfit from the try-on instead.</summary>
    public ulong CustomizeHash { get; private set; }
    public OutfitSnapshot Outfit { get; private set; } = OutfitSnapshot.Empty;
    public byte Race { get; private set; } = 1;
    public byte Sex { get; private set; }

    /// <summary>The player's idle pose (C-Pose index); a change alone raises <see cref="PoseChanged"/>.</summary>
    public byte IdlePose { get; private set; }
    public bool IsValid => Hash != 0;

    /// <summary>The look (customize data or outfit) changed: every rendered tile is outdated.</summary>
    public event Action? Changed;

    /// <summary>Only the idle pose changed: tiles keyed by the pose are re-requested, the others stay valid.</summary>
    public event Action? PoseChanged;

    public AppearanceTracker()
    {
        Plugin.Framework.Update += OnUpdate;
    }

    /// <summary>The local player's character, or null when not available.</summary>
    public static Character* LocalCharacter()
    {
        var player = Plugin.ObjectTable.LocalPlayer;
        return player == null ? null : (Character*)player.Address;
    }

    private void OnUpdate(IFramework framework)
    {
        if (DateTime.UtcNow < nextCheck)
            return;
        nextCheck = DateTime.UtcNow.AddSeconds(1);

        var character = LocalCharacter();
        if (character == null)
            return;

        var outfit = OutfitSnapshot.FromEquipped();
        var customize = &character->DrawData.CustomizeData;
        var hash = Fnv1a(new ReadOnlySpan<byte>(customize, sizeof(CustomizeData)), outfit);
        var pose = PlayerState.Instance()->CurrentPose(EmoteController.PoseType.Idle);
        if (hash == Hash && pose == IdlePose)
            return;

        var lookChanged = hash != Hash;
        IdlePose = pose;
        Hash = hash;
        CustomizeHash = Fnv1a(new ReadOnlySpan<byte>(customize, sizeof(CustomizeData)), OutfitSnapshot.Empty);
        Outfit = outfit;
        Race = customize->Race;
        Sex = customize->Sex;
        // A look change re-requests all tiles anyway, the pose included.
        if (lookChanged)
            Changed?.Invoke();
        else
            PoseChanged?.Invoke();
    }

    private static ulong Fnv1a(ReadOnlySpan<byte> customize, OutfitSnapshot outfit)
    {
        const ulong prime = 1099511628211;
        var hash = 14695981039346656037;
        void Mix(ulong value)
        {
            hash ^= value;
            hash *= prime;
        }

        foreach (var b in customize)
            Mix(b);
        foreach (var e in outfit.Entries)
        {
            Mix((byte)e.Slot);
            Mix(e.ItemId);
            Mix(e.Stain0);
            Mix(e.Stain1);
        }
        return hash;
    }

    public void Dispose() => Plugin.Framework.Update -= OnUpdate;
}
