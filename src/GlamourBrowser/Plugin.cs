using Dalamud.Game.ClientState.Conditions;
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
    private bool paused;
    private GlamourItem? selectionBeforePause;

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
            new DetailPanel(browser, ownership, live, config));
        settingsWindow = new SettingsWindow(config, browser, pool);
        windowSystem.AddWindow(mainWindow);
        windowSystem.AddWindow(settingsWindow);

        CommandManager.AddHandler(CommandName, new CommandInfo(OnCommand)
        {
            HelpMessage = "Öffnet den GlamourBrowser. /gbrowser diag schreibt den Waffenzustand aller Ansichten ins Log.",
        });

        PluginInterface.UiBuilder.Draw += windowSystem.Draw;
        PluginInterface.UiBuilder.OpenMainUi += mainWindow.Toggle;
        PluginInterface.UiBuilder.OpenConfigUi += settingsWindow.Toggle;
    }

    private void OnCommand(string command, string args)
    {
        if (args.Trim() != "diag")
        {
            mainWindow.Toggle();
            return;
        }

        Framework.RunOnFrameworkThread(() =>
        {
            lock (RenderSync.Lock)
            {
                Log.Information($"[diag] live: {live.DescribeWeapons()}");
                pool.WeaponDiagnosticsBudget = 6;
                foreach (var line in pool.DescribeWeapons())
                    Log.Information($"[diag] pool: {line}");
            }
        });
    }

    private CameraPreset? CameraFor(EquipSlot slot) => new CameraCalibrations(config.CameraCalibrations).Get(PlayerState.ContentId, slot);

    private void OnFrameworkUpdate(IFramework framework)
    {
        lock (RenderSync.Lock)
        {
            var pause = ShouldPause();
            if (pause && !paused)
            {
                paused = true;
                selectionBeforePause = live.Selected;
                live.Select(null);
                live.Tick();
                pool.Pause();
            }
            else if (!pause && paused)
            {
                paused = false;
                pool.Resume();
                live.Select(selectionBeforePause);
                browser.Refresh();
            }

            if (paused)
                return;
            pool.Tick();
            live.Tick();
        }
    }

    /// <summary>CharaViews are released during zone changes, cutscenes, GPose and while logged out.</summary>
    private static bool ShouldPause()
        => ObjectTable.LocalPlayer == null
           || ClientState.IsGPosing
           || Condition[ConditionFlag.BetweenAreas]
           || Condition[ConditionFlag.BetweenAreas51]
           || Condition[ConditionFlag.OccupiedInCutSceneEvent]
           || Condition[ConditionFlag.WatchingCutscene]
           || Condition[ConditionFlag.WatchingCutscene78];

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
