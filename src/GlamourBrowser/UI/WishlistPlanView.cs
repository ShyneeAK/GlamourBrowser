using System.Linq;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Utility.Raii;
using GlamourBrowser.Browser;
using GlamourBrowser.Core.Items;
using GlamourBrowser.Core.Localization;
using GlamourBrowser.Core.Sources;
using GlamourBrowser.Core.Wishlist;
using GlamourBrowser.Ownership;
using GlamourBrowser.Rendering;
using GlamourBrowser.Sources;
using GlamourBrowser.UI.Widgets;
using GlamourBrowser.Wishlist;
using GlamourBrowser.CustomSets;

namespace GlamourBrowser.UI;

/// <summary>
/// The wishlist's missing pieces grouped by source ("Nach Quelle"), to see where a run pays off. A group's action sits in
/// its header when all pieces share it, otherwise (recipes, store products) on each piece next to its cost; a click on a
/// piece shows it in the live preview and the item card.
/// </summary>
public sealed class WishlistPlanView
{
    private readonly BrowserState state;
    private readonly TryOnService tryOn;
    private readonly OwnershipService ownership;
    private readonly SourceService sources;
    private readonly LivePreview live;
    private readonly Theme.Theme theme;
    private WishPlan? plan;
    // Bumped by every event that can change the plan; the plan is rebuilt on the next draw, not every frame.
    private int version;
    private int planVersion;

    public WishlistPlanView(BrowserState state, OwnershipService ownership, WishlistService wishlist, SourceService sources, LivePreview live,
                            Theme.Theme theme, CustomSetService customSets, TryOnService tryOn)
    {
        this.tryOn = tryOn;
        this.state = state;
        this.ownership = ownership;
        this.sources = sources;
        this.live = live;
        this.theme = theme;
        ownership.Changed += () => version++;
        wishlist.Changed += () => version++;
        sources.Ready += () => version++;
        // A custom set on the wishlist changes the plan with its pieces.
        customSets.Changed += () => version++;
    }

    /// <param name="editor">While a custom set is edited, a click on a piece puts it in (as on a tile).</param>
    public void Draw(CustomSetEditor editor)
    {
        // Without the index every piece would land under "Quelle unbekannt".
        if (!sources.IsReady)
        {
            ImGui.TextDisabled(Loc.T("Quellen werden geladen …"));
            return;
        }

        if (plan == null || planVersion != version)
        {
            plan = WishPlanner.Plan(state.WishlistEntries, IsOwned, sources.Index.SourcesOf);
            planVersion = version;
        }

        if (plan.Missing == 0)
        {
            ImGui.TextDisabled(Loc.T("Alles erledigt"));
            return;
        }
        ImGui.TextUnformatted(Loc.F("Fehlend {0} von {1}", plan.Missing, plan.Total));

        using var child = ImRaii.Child("##wishplan", Vector2.Zero, false);
        if (!child.Success)
            return;

        var iconSize = new Vector2(ImGui.GetFrameHeight());
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
                    DrawPiece(piece, group.SharedAction == null, iconSize, editor);
            }
        }
    }

    private bool IsOwned(uint itemId) => ownership.Current?.IsOwned(itemId) ?? false;

    /// <summary>Icon, "Kategorie · Titel · Detail", the number of pieces and the shared action; the unknown group has no icon and no action.</summary>
    private void DrawHeader(WishGroup group, Vector2 iconSize)
    {
        var count = group.Pieces.Count == 1 ? Loc.T("1 Teil") : Loc.F("{0} Teile", group.Pieces.Count);
        ImGui.AlignTextToFramePadding();
        if (group.Source is not { } source)
        {
            ImGui.TextUnformatted(Loc.T("Quelle unbekannt"));
            ImGui.SameLine();
            ImGui.TextDisabled(count);
            return;
        }

        var iconMin = ImGui.GetCursorScreenPos();
        ImGui.Dummy(iconSize);
        TileGrid.DrawSourceIcon(ImGui.GetWindowDrawList(), source.Category, iconMin, iconSize.X, theme.Colors.TextMuted);
        ImGui.SameLine();
        ImGui.AlignTextToFramePadding();
        ImGui.TextUnformatted(string.Join(" · ", new[] { SourceCategories.DisplayName(source.Category), source.Title, source.Detail }.Where(t => t.Length > 0)));
        ImGui.SameLine();
        ImGui.TextDisabled(count);
        LivePane.DrawAction(group.SharedAction, "group", theme);
    }

    /// <summary>
    /// Icon and name (a click selects it, or puts it into the edited custom set), then the piece's own cost and, when
    /// the group has no shared one, its action.
    /// </summary>
    private void DrawPiece(WishPiece piece, bool ownAction, Vector2 iconSize, CustomSetEditor editor)
    {
        var item = piece.Item;
        // The row is a frame high (icon, action button), the name centred in it.
        ImGui.AlignTextToFramePadding();
        GameIcon.Draw(item.IconId, iconSize, FontAwesomeIcon.Tshirt, theme.Colors.TextMuted);
        ImGui.SameLine();
        var selected = state.Selected is ItemSubject { Item.Id: var selectedId } && selectedId == item.Id;
        // As wide as the name only, so cost and action fit behind it.
        if (ImGui.Selectable($"{item.Name}##piece{item.Id}", selected, ImGuiSelectableFlags.None, new Vector2(ImGui.CalcTextSize(item.Name).X, 0))
            && state.ItemSubjectFor(item.Id) is { } subject)
        {
            if (editor.IsOpen)
                editor.Put(subject);
            else
            {
                state.Selected = subject;
                live.Select(subject);
                tryOn.Put(subject, state.TileDye);
            }
        }

        if (piece.Source is not { } source)
            return;
        LivePane.DrawCost(source, theme);
        if (ownAction)
            LivePane.DrawAction(source.Action, $"piece{item.Id}", theme);
    }
}
