using System;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Game.Addon.Lifecycle;
using Dalamud.Game.Addon.Lifecycle.AddonArgTypes;
using FFXIVClientStructs.FFXIV.Client.Enums;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Component.GUI;

namespace GlamourBrowser.UI;

/// <summary>
/// The game's own item tooltip (ItemDetail) for an item the mouse rests on in our window: hung on _ScreenText while the
/// item is hovered, and gone a frame after the mouse leaves. The game draws it under the ImGui windows, so it is moved
/// beside the main window: left of it where it fits, else right. Other plugins' lines (market prices) come along.
/// </summary>
public sealed unsafe class NativeItemTooltip : IDisposable
{
    private const string HostAddon = "_ScreenText";
    private const string TooltipAddon = "ItemDetail";
    // Gap between the main window and the tooltip.
    private const float Gap = 8;

    private uint hovered;
    private Vector2 hoveredAt;
    private Vector2 windowMin;
    private Vector2 windowMax;
    // The item whose tooltip is open; 0 for none.
    private uint shown;

    public NativeItemTooltip()
    {
        foreach (var placed in Placed)
            Plugin.AddonLifecycle.RegisterListener(placed, TooltipAddon, Place);
    }

    // The game places the tooltip in its update; moved after that and once more before it is drawn.
    private static readonly AddonEvent[] Placed = [AddonEvent.PostUpdate, AddonEvent.PostRequestedUpdate, AddonEvent.PreDraw];

    /// <summary>The main window's screen rectangle this frame, which the tooltip keeps clear of.</summary>
    public void Avoid(Vector2 min, Vector2 max) => (windowMin, windowMax) = (min, max);

    /// <summary>Call while the item's icon is hovered; <paramref name="at"/> is the icon's top (the tooltip's height there).</summary>
    public void Hover(uint itemId, Vector2 at) => (hovered, hoveredAt) = (itemId, at);

    /// <summary>Once per frame after every window drew: shows the tooltip of the hovered item, else hides it.</summary>
    public void Update()
    {
        if (hovered != 0)
            Show(hovered);
        else
            Hide();
        hovered = 0;
    }

    public void Dispose()
    {
        foreach (var placed in Placed)
            Plugin.AddonLifecycle.UnregisterListener(placed, TooltipAddon, Place);
        Hide();
    }

    private static AtkUnitBase* Host() => RaptureAtkUnitManager.Instance()->GetAddonByName(HostAddon);

    // Only when it is not open for the item: every call places it anew where the game wants it, over our position.
    private void Show(uint itemId)
    {
        var open = RaptureAtkUnitManager.Instance()->GetAddonByName(TooltipAddon);
        if (shown == itemId && open != null && open->IsVisible)
            return;
        var host = Host();
        if (host == null || host->RootNode == null)
            return;
        var args = stackalloc AtkTooltipManager.AtkTooltipArgs[1];
        args->Ctor();
        args->ItemArgs.Kind = DetailKind.ItemId;
        args->ItemArgs.ItemId = (int)itemId;
        AtkStage.Instance()->TooltipManager.ShowTooltip(AtkTooltipType.Item, host->Id, host->RootNode, args);
        shown = itemId;
    }

    private void Hide()
    {
        if (shown == 0)
            return;
        shown = 0;
        var host = Host();
        if (host != null)
            AtkStage.Instance()->TooltipManager.HideTooltip(host->Id);
    }

    // Beside the main window, its top at the icon, kept on the screen.
    private void Place(AddonEvent type, AddonArgs args)
    {
        var tooltip = (AtkUnitBase*)args.Addon.Address;
        if (shown == 0 || tooltip == null || tooltip->RootNode == null)
            return;
        var width = tooltip->GetScaledWidth(true);
        var height = tooltip->GetScaledHeight(true);
        var screen = ImGui.GetIO().DisplaySize;
        var x = windowMin.X - Gap - width >= 0 ? windowMin.X - Gap - width : windowMax.X + Gap;
        x = Math.Clamp(x, 0, MathF.Max(0, screen.X - width));
        var y = Math.Clamp(hoveredAt.Y, 0, MathF.Max(0, screen.Y - height));
        var (newX, newY) = ((short)MathF.Round(x), (short)MathF.Round(y));
        if (tooltip->X != newX || tooltip->Y != newY)
            tooltip->SetPosition(newX, newY);
    }
}
