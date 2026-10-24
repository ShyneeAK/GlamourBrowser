using System;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game.Character;

namespace GlamourBrowser.Rendering;

/// <summary>Watches the local player's look (customize data + worn outfit) once per second.</summary>
public sealed unsafe class AppearanceTracker : IDisposable
{
    private DateTime nextCheck = DateTime.MinValue;

    public ulong Hash { get; private set; }
    public OutfitSnapshot Outfit { get; private set; } = OutfitSnapshot.Empty;
    public byte Race { get; private set; } = 1;
    public byte Sex { get; private set; }
    public bool IsValid => Hash != 0;

    public event Action? Changed;

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
        if (hash == Hash)
            return;

        Hash = hash;
        Outfit = outfit;
        Race = customize->Race;
        Sex = customize->Sex;
        Changed?.Invoke();
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
