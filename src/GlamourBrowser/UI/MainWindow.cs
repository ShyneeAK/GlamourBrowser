using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Windowing;
using GlamourBrowser.Core.Items;

namespace GlamourBrowser.UI;

public sealed class MainWindow : Window
{
    private readonly IReadOnlyList<GlamourItem> catalog;

    public MainWindow(IReadOnlyList<GlamourItem> catalog)
        : base("GlamourBrowser##GlamourBrowser")
    {
        this.catalog = catalog;
        SizeConstraints = new WindowSizeConstraints { MinimumSize = new Vector2(900, 600) };
    }

    public override void Draw()
    {
        // Temporary until the real UI (plan task 10).
        ImGui.Text($"Katalog: {catalog.Count} Items");
        foreach (var group in catalog.GroupBy(i => i.Slot).OrderBy(g => g.Key))
            ImGui.Text($"{group.Key}: {group.Count()}");
    }
}
