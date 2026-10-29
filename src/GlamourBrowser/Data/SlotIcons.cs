using System.Collections.Generic;
using System.Numerics;
using GlamourBrowser.Core.Items;
using GlamourBrowser.UI.Widgets;
using Lumina.Excel.Sheets;

namespace GlamourBrowser.Data;

/// <summary>Slot icons taken from the game's item categories (ItemUICategory.Icon).</summary>
public static class SlotIcons
{
    private static readonly Dictionary<EquipSlot, uint> CategoryRows = new()
    {
        [EquipSlot.MainHand] = 2,   // Gladiator's arm
        [EquipSlot.OffHand] = 11,   // Shield
        [EquipSlot.Head] = 34,
        [EquipSlot.Body] = 35,
        [EquipSlot.Hands] = 37,
        [EquipSlot.Legs] = 36,
        [EquipSlot.Feet] = 38,
        [EquipSlot.Ears] = 41,
        [EquipSlot.Neck] = 40,
        [EquipSlot.Wrists] = 42,
        [EquipSlot.RingRight] = 43,
        [EquipSlot.RingLeft] = 43,
    };

    private static readonly Dictionary<EquipSlot, uint> Cache = new();

    /// <summary>The silver chest among the item category icons, in the style of the slot icons.</summary>
    public const uint Sets = 60190;

    /// <summary>The copper star of the item categories ("Anderes").</summary>
    public const uint Wishlist = 60159;

    /// <summary>The main menu's "Inhaltsrückblende" (a turning arrow around a play symbol), for "Zuletzt benutzt".</summary>
    public const uint History = 73;

    /// <summary>The emote "Herzpose" (hands forming a heart), for the favourites.</summary>
    public const uint Favorites = 246423;

    /// <summary>The "Projektionsplatte" button of the game's character window.</summary>
    public static readonly UiIcon MySets = UiIcon.Part("ui/uld/CharacterGearSet_hr1.tex", new Vector2(0, 0.5f), new Vector2(1 / 3f, 1));

    public static uint IconFor(EquipSlot slot)
    {
        if (Cache.TryGetValue(slot, out var icon))
            return icon;
        icon = CategoryRows.TryGetValue(slot, out var row) && Plugin.DataManager.GetExcelSheet<ItemUICategory>().TryGetRow(row, out var category)
            ? (uint)category.Icon
            : 0;
        return Cache[slot] = icon;
    }
}
