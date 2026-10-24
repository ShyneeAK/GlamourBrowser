using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Windowing;
using GlamourBrowser.Core.Items;
using GlamourBrowser.Ownership;

namespace GlamourBrowser.UI;

public sealed class MainWindow : Window
{
    private readonly IReadOnlyList<GlamourItem> catalog;
    private readonly OwnershipService ownership;

    public MainWindow(IReadOnlyList<GlamourItem> catalog, OwnershipService ownership)
        : base("GlamourBrowser##GlamourBrowser")
    {
        this.catalog = catalog;
        this.ownership = ownership;
        SizeConstraints = new WindowSizeConstraints { MinimumSize = new Vector2(900, 600) };
    }

    public override void Draw()
    {
        // Temporary until the real UI (plan task 10).
        ImGui.Text($"Katalog: {catalog.Count} Items");
        foreach (var group in catalog.GroupBy(i => i.Slot).OrderBy(g => g.Key))
            ImGui.Text($"{group.Key}: {group.Count()}");

        ImGui.Separator();
        if (ownership.Current is not { } store)
        {
            ImGui.Text("Nicht eingeloggt");
            return;
        }
        ImGui.Text($"Besitz für {store.ContentId}:");
        foreach (var c in store.Containers.OrderBy(c => c.Id.Kind).ThenBy(c => c.DisplayName))
            ImGui.Text($"{c.DisplayName}: {c.ItemIds.Count} Items, Stand {c.Seen:dd.MM. HH:mm}");
    }
}
