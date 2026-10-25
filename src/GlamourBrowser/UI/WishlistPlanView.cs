using System.Linq;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Utility.Raii;
using GlamourBrowser.Browser;
using GlamourBrowser.Core.Items;
using GlamourBrowser.Core.Sources;
using GlamourBrowser.Core.Wishlist;
using GlamourBrowser.Ownership;
using GlamourBrowser.Rendering;
using GlamourBrowser.Sources;
using GlamourBrowser.UI.Widgets;
using GlamourBrowser.Wishlist;

namespace GlamourBrowser.UI;

/// <summary>
/// The wishlist's missing pieces grouped by source ("Nach Quelle"), to see where a run pays off; each group with the
/// source's action, a click on a piece shows it in the live preview and the item card.
/// </summary>
public sealed class WishlistPlanView
{
    private readonly BrowserState state;
    private readonly OwnershipService ownership;
    private readonly SourceService sources;
    private readonly LivePreview live;
    private readonly Theme.Theme theme;
    private WishPlan? plan;
    // Bumped by every event that can change the plan; the plan is rebuilt on the next draw, not every frame.
    private int version;
    private int planVersion;

    public WishlistPlanView(BrowserState state, OwnershipService ownership, WishlistService wishlist, SourceService sources, LivePreview live,
                            Theme.Theme theme)
    {
        this.state = state;
        this.ownership = ownership;
        this.sources = sources;
        this.live = live;
        this.theme = theme;
        ownership.Changed += () => version++;
        wishlist.Changed += () => version++;
        sources.Ready += () => version++;
    }

    public void Draw()
    {
        if (plan == null || planVersion != version)
        {
            plan = WishPlanner.Plan(state.WishlistEntries, IsOwned, sources.Index.SourcesOf);
            planVersion = version;
        }

        if (plan.Missing == 0)
        {
            ImGui.TextDisabled("Alles erledigt");
            return;
        }
        ImGui.TextUnformatted($"Fehlend {plan.Missing} von {plan.Total}");

        using var child = ImRaii.Child("##wishplan", Vector2.Zero, false);
        if (!child.Success)
            return;

        var iconSize = new Vector2(ImGui.GetTextLineHeight());
        for (var i = 0; i < plan.Groups.Count; i++)
        {
            using var id = ImRaii.PushId(i);
            if (i > 0)
                ImGui.Separator();
            var group = plan.Groups[i];
            DrawHeader(group, iconSize);
            using (ImRaii.PushIndent(iconSize.X + ImGui.GetStyle().ItemSpacing.X))
            {
                foreach (var piece in group.Pieces)
                    DrawPiece(piece, iconSize);
            }
        }
    }

    private bool IsOwned(uint itemId) => ownership.Current?.IsOwned(itemId) ?? false;

    /// <summary>Icon, "Kategorie · Titel · Detail", the number of pieces, cost and action; the unknown group has no icon and no action.</summary>
    private void DrawHeader(WishGroup group, Vector2 iconSize)
    {
        var count = group.Pieces.Count == 1 ? "1 Teil" : $"{group.Pieces.Count} Teile";
        if (group.Source is not { } source)
        {
            ImGui.TextUnformatted("Quelle unbekannt");
            ImGui.SameLine();
            ImGui.TextDisabled(count);
            return;
        }

        var iconMin = ImGui.GetCursorScreenPos();
        ImGui.Dummy(iconSize);
        TileGrid.DrawSourceIcon(ImGui.GetWindowDrawList(), source.Category, iconMin, iconSize.X, theme.Colors.TextMuted);
        ImGui.SameLine();
        ImGui.TextUnformatted(string.Join(" · ", new[] { SourceCategories.DisplayName(source.Category), source.Title, source.Detail }.Where(t => t.Length > 0)));
        ImGui.SameLine();
        ImGui.TextDisabled(count);
        LivePane.DrawCostAndAction(source, "group", theme);
    }

    private void DrawPiece(GlamourItem piece, Vector2 iconSize)
    {
        GameIcon.Draw(piece.IconId, iconSize, FontAwesomeIcon.Tshirt, theme.Colors.TextMuted);
        ImGui.SameLine();
        var selected = state.Selected is ItemSubject { Item.Id: var selectedId } && selectedId == piece.Id;
        if (ImGui.Selectable($"{piece.Name}##piece{piece.Id}", selected) && state.ItemSubjectFor(piece.Id) is { } subject)
        {
            state.Selected = subject;
            live.Select(subject);
        }
    }
}
