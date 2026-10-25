using Dalamud.Game.ClientState.Conditions;
using Dalamud.Game.Command;
using Dalamud.Interface.Windowing;
using Dalamud.IoC;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using Dalamud.Interface.Textures.TextureWraps;
using GlamourBrowser.Browser;
using GlamourBrowser.Core.Items;
using GlamourBrowser.Core.Rendering;
using GlamourBrowser.Data;
using GlamourBrowser.Ownership;
using GlamourBrowser.Rendering;
using GlamourBrowser.Sources;
using GlamourBrowser.UI;
using GlamourBrowser.Wishlist;
using GlamourBrowser.CustomSets;

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
    [PluginService] internal static IChatGui ChatGui { get; private set; } = null!;
    [PluginService] internal static IGameGui GameGui { get; private set; } = null!;

    private const string CommandName = "/gbrowser";

    private readonly WindowSystem windowSystem = new("GlamourBrowser");
    private readonly MainWindow mainWindow;
    private readonly SettingsWindow settingsWindow;
    private readonly OwnershipService ownership;
    private readonly WishlistService wishlist;
    private readonly CustomSetService customSets;
    private readonly GlamourPlate.GlamourPlateBridge plates;
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
    private readonly CalibrationService calibration;
    private readonly SourceService sources;
    private readonly MemoryStats memoryStats;
    private bool paused;

    public Plugin()
    {
        config = PluginInterface.GetPluginConfig() as Configuration ?? new Configuration();
        if (config.Version < 2)
        {
            // Saved before the page cache default went up: the old default would stay otherwise.
            config.CachedPages = 8;
            config.Version = 2;
            config.Save();
        }
        calibration = new CalibrationService(config);
        var (catalog, variants) = ItemCatalogLoader.Load(DataManager);
        ownership = new OwnershipService(variants);
        wishlist = new WishlistService(variants);
        customSets = new CustomSetService(variants);
        inventoryReader = new InventoryReader(ownership);
        retainerReader = new RetainerReader(ownership);
        dresserReader = new DresserReader(ownership);
        armoireReader = new ArmoireReader(ownership);
        appearance = new AppearanceTracker();
        var byId = catalog.ToDictionary(i => i.Id);
        // The browser state is created below; the pool asks it only once it ticks.
        var jobs = new JobCatalog(DataManager);
        byte? StanceJobOf(GlamourItem item) => StanceJob.ForItem(item, jobs.ParentOf);
        pool = new RenderPool(queue, cache, key => browser?.SubjectFor(key), appearance, CameraFor, StanceJobOf) { MaxViews = config.ParallelViews };
        live = new LivePreview(pool, appearance, config, CameraFor, StanceJobOf);
        // Variants too: their sources are merged into the base item.
        sources = new SourceService(catalog.Select(i => i.Id).Concat(variants.Keys).ToHashSet());
        var sets = new SetCatalog(byId, variants, sources);
        var dyes = new DyeCatalog(DataManager);
        browser = new BrowserState(catalog, ownership, appearance, queue, cache, pool, config, sources, sets, wishlist, jobs, customSets);
        UI.UiScale.Set(config.UiScale);
        var theme = new UI.Theme.Theme(config);
        var editor = new CustomSetEditor(browser, customSets, live, variants, appearance);
        memoryStats = new MemoryStats(cache);
        // Before the main window: its title bar button opens it.
        settingsWindow = new SettingsWindow(config, browser, pool, calibration, theme);
        plates = new GlamourPlate.GlamourPlateBridge(variants, ownership, dyes, id => browser.ItemOf(id)?.Name ?? $"Item {id}",
            id => browser.ItemOf(id)?.DyeCount ?? 0);
        mainWindow = new MainWindow(
            new LivePane(browser, ownership, live, calibration, jobs, theme, sources, wishlist, customSets, dyes, editor,
                         new SetEditorPane(editor, browser, dyes, theme, plates), plates),
            new TopBar(browser, jobs, config, theme),
            new FilterColumn(browser, ownership, config, sources, theme),
            new TileGrid(browser, cache, pool, ownership, live, config, theme, sources, jobs, wishlist,
                         new WishlistPlanView(browser, ownership, wishlist, sources, live, theme, customSets), customSets, editor, plates,
                         memoryStats),
            theme,
            config,
            settingsWindow.Toggle);
        windowSystem.AddWindow(mainWindow);
        windowSystem.AddWindow(settingsWindow);

        CommandManager.AddHandler(CommandName, new CommandInfo(OnCommand)
        {
            HelpMessage = "Öffnet den GlamourBrowser. /gbrowser diag schreibt den Waffenzustand aller Ansichten ins Log, /gbrowser plate den Zustand des Glamour-Plate-Editors.",
        });

        // Only now: the framework handler uses the windows and everything above (a tick during construction would find them null).
        Framework.Update += OnFrameworkUpdate;
        PluginInterface.UiBuilder.Draw += windowSystem.Draw;
        PluginInterface.UiBuilder.OpenMainUi += mainWindow.Toggle;
        PluginInterface.UiBuilder.OpenConfigUi += settingsWindow.Toggle;
    }

    private void OnCommand(string command, string args)
    {
        var parts = args.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length > 0 && parts[0] == "plate")
        {
            Framework.RunOnFrameworkThread(() => GlamourPlate.PlateDiagnostics.Run(parts[1..]));
            return;
        }
        if (parts.Length == 0 || parts[0] != "diag")
        {
            mainWindow.Toggle();
            return;
        }

        Framework.RunOnFrameworkThread(() =>
        {
            lock (RenderSync.Lock)
            {
                Log.Information($"[diag] live: {live.DescribeWeapons()}");
                unsafe
                {
                    // Reference: the game's own try-on window, which shows weapons correctly.
                    var tryon = FFXIVClientStructs.FFXIV.Client.UI.Agent.AgentTryon.Instance();
                    var tryonView = tryon != null ? (FFXIVClientStructs.FFXIV.Client.UI.Misc.CharaView*)&tryon->CharaView : null;
                    Log.Information(tryonView != null && tryonView->State == 6
                        ? $"[diag] tryon: draw {tryon->CharaView.DrawWeapon}, hidden {tryon->CharaView.WeaponHidden} | {CharaViewHandle.DescribeCharacter(tryonView->GetCharacter())}"
                        : "[diag] tryon: not open");
                }
                pool.WeaponDiagnosticsBudget = 6;
                foreach (var line in pool.DescribeWeapons())
                    Log.Information($"[diag] pool: {line}");
            }
        });
    }

    private CameraPreset? CameraFor(EquipSlot slot) => calibration.Get(PlayerState.ContentId, slot);

    private void OnFrameworkUpdate(IFramework framework)
    {
        lock (RenderSync.Lock)
        {
            // Closed window: release everything like a zone change, so nothing renders in the background.
            var pause = ShouldPause() || !mainWindow.IsOpen;
            if (pause && !paused)
            {
                paused = true;
                live.Release();
                pool.Pause();
            }
            else if (!pause && paused)
            {
                paused = false;
                pool.Resume();
                // The current selection, not the one from before the pause: a custom set may be gone (logout, other character).
                live.Select(browser.Selected);
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
        UI.UiScale.Dispose();

        Framework.Update -= OnFrameworkUpdate;
        plates.Dispose();
        // A source build still running at unload must not hand its index to the parts released below.
        sources.Dispose();
        // CharaViews must be released on the framework thread.
        Framework.RunOnFrameworkThread(() =>
        {
            live.Dispose();
            pool.Dispose();
            cache.Dispose();
        }).Wait();
        memoryStats.Dispose();
        appearance.Dispose();

        armoireReader.Dispose();
        dresserReader.Dispose();
        retainerReader.Dispose();
        inventoryReader.Dispose();
        customSets.Dispose();
        wishlist.Dispose();
        ownership.Dispose();
    }
}
