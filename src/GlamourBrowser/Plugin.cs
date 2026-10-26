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
using GlamourBrowser.Core.CustomSets;
using GlamourBrowser.Core.Items;
using GlamourBrowser.Core.Rendering;
using GlamourBrowser.Data;
using GlamourBrowser.Ownership;
using GlamourBrowser.Rendering;
using GlamourBrowser.Sources;
using GlamourBrowser.UI;
using GlamourBrowser.Wishlist;
using GlamourBrowser.CustomSets;
using GlamourBrowser.Core.Localization;

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

    private const string CommandName = "/glamourdresser";
    private const string ShortCommandName = "/sgd";

    private readonly WindowSystem windowSystem = new("ShyneesGlamourDresser");
    private readonly MainWindow mainWindow;
    private readonly SettingsWindow settingsWindow;
    private readonly OwnershipService ownership;
    private readonly WishlistService wishlist;
    private readonly CustomSetService customSets;
    private readonly TryOnService tryOn;
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
    // Frames left of a /glamourdresser trace (live view and the game's try-on window, every third frame).
    private int traceFrames;

    public Plugin()
    {
        config = PluginInterface.GetPluginConfig() as Configuration ?? new Configuration();
        if (config.Version < 3)
        {
            // Saved before the page cache default went up: an old default would stay otherwise (a chosen value stays).
            if (config.Version < 2 || config.CachedPages == 8)
                config.CachedPages = 10;
            config.Version = 3;
            config.Save();
        }
        if (config.Version < 4)
        {
            // Parallel views left the normal settings: everyone gets the default back.
            config.ParallelViews = 3;
            config.Version = 4;
            config.Save();
        }
        // First: texts built from here on (slot names, labels) are in this language.
        Loc.Load(LanguageOf(config.Language));
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
        // What an item blocks besides its own slot (a robe over legs and feet); worn "+N" variants block like their base item.
        ushort BlocksOf(uint id) => byId.GetValueOrDefault(variants.GetValueOrDefault(id, id))?.Blocks ?? 0;
        tryOn = new TryOnService(appearance, config, BlocksOf);
        // The browser state is created below; the pool asks it only once it ticks.
        pool = new RenderPool(queue, cache, key => browser?.SubjectFor(key), appearance, CameraFor, tryOn.TryOn.LookFor,
                              id => browser?.ItemOf(id)) { MaxViews = config.ParallelViews };
        live = new LivePreview(pool, appearance, config, CameraFor);
        // Variants too: their sources are merged into the base item.
        sources = new SourceService(catalog.Select(i => i.Id).Concat(variants.Keys).ToHashSet());
        var sets = new SetCatalog(byId, variants, sources);
        var jobs = new JobCatalog(DataManager);
        var dyes = new DyeCatalog(DataManager);
        browser = new BrowserState(catalog, ownership, appearance, queue, cache, pool, config, sources, sets, wishlist, jobs, customSets, tryOn);
        UI.UiScale.Set(config.UiScale);
        var theme = new UI.Theme.Theme(config);
        var editor = new CustomSetEditor(browser, customSets, live, variants, appearance, BlocksOf);
        live.ItemOf = browser.ItemOf;
        // The live view wears the try-on; while a set is edited its draft (known pieces only) with the basis's weapons where
        // it has none, like an own design's tile: its empty slots stay empty. Built again only when the basis or the draft
        // changed (both hand out a new list then).
        (IReadOnlyList<CustomPiece> Basis, IReadOnlyList<CustomPiece> Draft, IReadOnlyList<CustomPiece> Look)? editorLook = null;
        live.Look = () =>
        {
            if (editor.Draft is not { } draft)
                return tryOn.TryOn.Pieces;
            if (editorLook is not { } cached || !ReferenceEquals(cached.Basis, tryOn.TryOn.Basis) || !ReferenceEquals(cached.Draft, draft.Pieces))
            {
                var known = draft.Pieces.Where(p => browser.ItemOf(p.ItemId) != null).ToList();
                var weapons = tryOn.TryOn.Basis.Where(p => !SlotMask.Contains(CustomSetSubjects.LookSlots, p.Slot)).ToList();
                editorLook = cached = (tryOn.TryOn.Basis, draft.Pieces, Looks.Overlay(weapons, known, BlocksOf));
            }
            return cached.Look;
        };
        memoryStats = new MemoryStats(cache);
        // Before the main window: its title bar button opens it.
        settingsWindow = new SettingsWindow(config, browser, pool, calibration, theme);
        plates = new GlamourPlate.GlamourPlateBridge(variants, ownership, dyes, id => browser.ItemOf(id)?.Name ?? Loc.F("Item {0}", id),
            id => browser.ItemOf(id)?.DyeCount ?? 0);
        var filterControls = new TileFilterControls(browser, jobs, dyes, theme);
        mainWindow = new MainWindow(
            new LivePane(browser, ownership, live, calibration, jobs, theme, sources, wishlist, customSets, dyes, editor,
                         new SetEditorPane(editor, browser, dyes, theme, plates), plates, tryOn),
            new TopBar(browser, config, theme, dyes, filterControls),
            new FilterColumn(browser, ownership, config, sources, theme),
            new TileGrid(browser, cache, pool, ownership, live, config, theme, sources, jobs, wishlist,
                         new WishlistPlanView(browser, ownership, wishlist, sources, live, theme, customSets, tryOn), customSets, editor, plates,
                         memoryStats, tryOn, filterControls),
            theme,
            config,
            settingsWindow.Toggle);
        windowSystem.AddWindow(mainWindow);
        windowSystem.AddWindow(settingsWindow);

        CommandManager.AddHandler(CommandName, new CommandInfo(OnCommand)
        {
            HelpMessage = Loc.T("Öffnet Shynees Glamour Dresser (kurz: /sgd)."),
        });
        CommandManager.AddHandler(ShortCommandName, new CommandInfo(OnCommand)
        {
            HelpMessage = Loc.T("Öffnet Shynees Glamour Dresser."),
            ShowInHelp = false,
        });

        // Only now: the framework handler uses the windows and everything above (a tick during construction would find them null).
        Framework.Update += OnFrameworkUpdate;
        PluginInterface.UiBuilder.Draw += windowSystem.Draw;
        PluginInterface.UiBuilder.OpenMainUi += mainWindow.Toggle;
        PluginInterface.UiBuilder.OpenConfigUi += settingsWindow.Toggle;
    }

    /// <summary>The chosen language, or the game client's when none is chosen.</summary>
    public static string LanguageOf(string chosen)
        => chosen.Length > 0 ? chosen : ClientState.ClientLanguage switch
        {
            Dalamud.Game.ClientLanguage.German => "de",
            Dalamud.Game.ClientLanguage.French => "fr",
            Dalamud.Game.ClientLanguage.Japanese => "ja",
            _ => "en",
        };

    private void OnCommand(string command, string args)
    {
        var parts = args.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length > 0 && parts[0] == "plate")
        {
            Framework.RunOnFrameworkThread(() => GlamourPlate.PlateDiagnostics.Run(parts[1..]));
            return;
        }
        if (parts.Length > 0 && parts[0] == "trace")
        {
            Framework.RunOnFrameworkThread(() =>
            {
                traceFrames = 300;
                // Weapon tiles too: one line each for the next ones copied.
                pool.WeaponDiagnosticsBudget = 40;
            });
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
                          + $" || {CharaViewHandle.DescribeModelData(tryonView)}"
                        : "[diag] tryon: not open");
                }
                pool.WeaponDiagnosticsBudget = 6;
                foreach (var line in pool.DescribeWeapons())
                    Log.Information($"[diag] pool: {line}");
            }
        });
    }

    private unsafe void Trace()
    {
        var frame = FFXIVClientStructs.FFXIV.Client.System.Framework.Framework.Instance()->FrameCounter;
        Log.Information($"[trace] {frame} live: {live.DescribeTrace()}");
        var tryon = FFXIVClientStructs.FFXIV.Client.UI.Agent.AgentTryon.Instance();
        if (tryon != null && tryon->CharaView.CharaView.State == 6)
            Log.Information($"[trace] {frame} tryon: draw {tryon->CharaView.DrawWeapon} | {CharaViewHandle.DescribeTrace(&tryon->CharaView.CharaView)}");
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
            if (traceFrames > 0 && traceFrames-- % 3 == 0)
                Trace();
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
        CommandManager.RemoveHandler(ShortCommandName);
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
        tryOn.Dispose();
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
