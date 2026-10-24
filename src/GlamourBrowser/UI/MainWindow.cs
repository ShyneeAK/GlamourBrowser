using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Textures;
using Dalamud.Interface.Textures.TextureWraps;
using Dalamud.Interface.Windowing;
using GlamourBrowser.Core.Items;
using GlamourBrowser.Core.Rendering;
using GlamourBrowser.Ownership;
using GlamourBrowser.Rendering;

namespace GlamourBrowser.UI;

public sealed class MainWindow : Window
{
    private readonly IReadOnlyList<GlamourItem> catalog;
    private readonly OwnershipService ownership;
    private readonly AppearanceTracker appearance;
    private readonly TileQueue queue;
    private readonly TileCache<IDalamudTextureWrap> cache;
    private List<TileKey> testKeys = new();

    public MainWindow(IReadOnlyList<GlamourItem> catalog, OwnershipService ownership, AppearanceTracker appearance,
                      TileQueue queue, TileCache<IDalamudTextureWrap> cache)
        : base("GlamourBrowser##GlamourBrowser")
    {
        this.catalog = catalog;
        this.ownership = ownership;
        this.appearance = appearance;
        this.queue = queue;
        this.cache = cache;
        SizeConstraints = new WindowSizeConstraints { MinimumSize = new Vector2(900, 600) };
    }

    public override void Draw()
    {
        // Temporary diagnostics until the real UI (plan task 10).
        ImGui.Text($"Katalog: {catalog.Count} Items | " + string.Join(", ", catalog.GroupBy(i => i.Slot).OrderBy(g => g.Key).Select(g => $"{g.Key} {g.Count()}")));

        if (ownership.Current is { } store)
        {
            foreach (var c in store.Containers.OrderBy(c => c.Id.Kind).ThenBy(c => c.DisplayName))
                ImGui.Text($"{c.DisplayName}: {c.ItemIds.Count} Items, Stand {c.Seen:dd.MM. HH:mm}");
        }
        else
        {
            ImGui.Text("Nicht eingeloggt");
        }

        ImGui.Separator();
        if (ImGui.Button("Test-Raster"))
            Plugin.Framework.RunOnFrameworkThread(StartTestGrid);
        ImGui.SameLine();
        ImGui.Text($"Warteschlange: {queue.Count} | Cache: {cache.Count}");

        for (var i = 0; i < testKeys.Count; i++)
        {
            if (i % 9 != 0)
                ImGui.SameLine();
            var size = new Vector2(96, 160);
            if (cache.TryGet(testKeys[i], out var texture) && texture != null)
                ImGui.Image(texture.Handle, size);
            else
                ImGui.Image(Plugin.TextureProvider.GetFromGameIcon(new GameIconLookup(catalog.First(c => c.Id == testKeys[i].ItemId).IconId)).GetWrapOrEmpty().Handle, new Vector2(96, 96));
        }
    }

    private void StartTestGrid()
    {
        testKeys = catalog.Where(i => i.Slot == EquipSlot.Body).Take(18)
            .Select(i => new TileKey(i.Id, 0, 0, TileMode.CurrentOutfit, appearance.Hash, 192))
            .ToList();
        cache.Retain(testKeys.ToHashSet());
        queue.SetRequests(testKeys.Where(k => !cache.TryGet(k, out _)), []);
    }
}
