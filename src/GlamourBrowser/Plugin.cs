using Dalamud.Game.Command;
using Dalamud.Interface.Windowing;
using Dalamud.IoC;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using System.Collections.Generic;
using System.Linq;
using Dalamud.Interface.Textures.TextureWraps;
using GlamourBrowser.Browser;
using GlamourBrowser.Core.Items;
using GlamourBrowser.Core.Rendering;
using GlamourBrowser.Data;
using GlamourBrowser.Ownership;
using GlamourBrowser.Rendering;
using GlamourBrowser.UI;

namespace GlamourBrowser;

public sealed class Plugin : IDalamudPlugin
{
    [PluginService] internal static IDalamudPluginInterface PluginInterface { get; private set; } = null!;
    [PluginService] internal static ICommandManager CommandManager { get; private set; } = null!;
    [PluginService] internal static IClientState ClientState { get; private set; } = null!;
    [PluginService] internal static IObjectTable ObjectTable { get; private set; } = null!;
    [PluginService] internal static IPlayerState PlayerState { get; private set; } = null!;
    [PluginService] internal static IDataManager DataManager { get; private set; } = null!;
    [PluginService] internal static IFramework Framework { get; private set; } = null!;
    [PluginService] internal static ITextureProvider TextureProvider { get; private set; } = null!;
    [PluginService] internal static IGameInventory GameInventory { get; private set; } = null!;
    [PluginService] internal static IAddonLifecycle AddonLifecycle { get; private set; } = null!;
    [PluginService] internal static ICondition Condition { get; private set; } = null!;
    [PluginService] internal static IPluginLog Log { get; private set; } = null!;

    private const string CommandName = "/gbrowser";

    private readonly WindowSystem windowSystem = new("GlamourBrowser");
    private readonly MainWindow mainWindow;
    private readonly SettingsWindow settingsWindow;
    private readonly OwnershipService ownership;
    private readonly InventoryReader inventoryReader;
    private readonly RetainerReader retainerReader;
    private readonly DresserReader dresserReader;
    private readonly ArmoireReader armoireReader;
    private readonly AppearanceTracker appearance;
    private readonly TileQueue queue = new();
    private readonly TileCache<IDalamudTextureWrap> cache = new();
    private readonly RenderPool pool;
    private readonly LivePreview live;
    private readonly BrowserState browser;
    private readonly Configuration config;

    public Plugin()
    {
        config = PluginInterface.GetPluginConfig() as Configuration ?? new Configuration();
        var catalog = ItemCatalogLoader.Load(DataManager);
        ownership = new OwnershipService();
        inventoryReader = new InventoryReader(ownership);
        retainerReader = new RetainerReader(ownership);
        dresserReader = new DresserReader(ownership);
        armoireReader = new ArmoireReader(ownership);
        appearance = new AppearanceTracker();
        var byId = catalog.ToDictionary(i => i.Id);
        pool = new RenderPool(queue, cache, id => byId.GetValueOrDefault(id), appearance, CameraFor) { MaxViews = config.ParallelViews };
        live = new LivePreview(pool, appearance, config, CameraFor);
        browser = new BrowserState(catalog, ownership, appearance, queue, cache, pool, config);
        Framework.Update += OnFrameworkUpdate;
        mainWindow = new MainWindow(
            new FilterPanel(browser, ownership),
            new TileGrid(browser, cache, pool, ownership, live, config),
            new DetailPanel(browser, ownership, live));
        settingsWindow = new SettingsWindow(config, browser, pool);
        windowSystem.AddWindow(mainWindow);
        windowSystem.AddWindow(settingsWindow);

        CommandManager.AddHandler(CommandName, new CommandInfo((_, _) => mainWindow.Toggle())
        {
            HelpMessage = "Öffnet den GlamourBrowser.",
        });

        PluginInterface.UiBuilder.Draw += windowSystem.Draw;
        PluginInterface.UiBuilder.OpenMainUi += mainWindow.Toggle;
        PluginInterface.UiBuilder.OpenConfigUi += settingsWindow.Toggle;
    }

    private CameraPreset CameraFor(EquipSlot slot) => CameraPresets.Resolve(slot, config.CameraOverrides);

    private void OnFrameworkUpdate(IFramework framework)
    {
        lock (RenderSync.Lock)
        {
            pool.Tick();
            live.Tick();
        }
    }

    public void Dispose()
    {
        // Stop drawing first: the UI reads the tile textures that are released below.
        PluginInterface.UiBuilder.Draw -= windowSystem.Draw;
        PluginInterface.UiBuilder.OpenMainUi -= mainWindow.Toggle;
        PluginInterface.UiBuilder.OpenConfigUi -= settingsWindow.Toggle;
        windowSystem.RemoveAllWindows();
        CommandManager.RemoveHandler(CommandName);

        Framework.Update -= OnFrameworkUpdate;
        // CharaViews must be released on the framework thread.
        Framework.RunOnFrameworkThread(() =>
        {
            live.Dispose();
            pool.Dispose();
            cache.Dispose();
        }).Wait();
        appearance.Dispose();

        armoireReader.Dispose();
        dresserReader.Dispose();
        retainerReader.Dispose();
        inventoryReader.Dispose();
        ownership.Dispose();
    }
}
