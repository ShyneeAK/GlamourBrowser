using Dalamud.Game.Command;
using Dalamud.Interface.Windowing;
using Dalamud.IoC;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;

namespace GlamourSpike;

public sealed class Plugin : IDalamudPlugin
{
    [PluginService] internal static IDalamudPluginInterface PluginInterface { get; private set; } = null!;
    [PluginService] internal static ITextureProvider TextureProvider { get; private set; } = null!;
    [PluginService] internal static ICommandManager CommandManager { get; private set; } = null!;
    [PluginService] internal static IClientState ClientState { get; private set; } = null!;
    [PluginService] internal static IObjectTable ObjectTable { get; private set; } = null!;
    [PluginService] internal static IDataManager DataManager { get; private set; } = null!;
    [PluginService] internal static IFramework Framework { get; private set; } = null!;
    [PluginService] internal static IPluginLog Log { get; private set; } = null!;

    private const string CommandName = "/gbspike";

    private readonly WindowSystem windowSystem = new("GlamourSpike");
    private readonly SpikeController controller;
    private readonly SpikeWindow window;

    public Plugin()
    {
        controller = new SpikeController();
        window = new SpikeWindow(controller);
        windowSystem.AddWindow(window);

        CommandManager.AddHandler(CommandName, new CommandInfo((_, _) => window.Toggle())
        {
            HelpMessage = "Oeffnet das GlamourBrowser-Spike-Fenster.",
        });

        PluginInterface.UiBuilder.Draw += windowSystem.Draw;
        PluginInterface.UiBuilder.OpenMainUi += window.Toggle;
        PluginInterface.UiBuilder.OpenConfigUi += window.Toggle;
        Framework.Update += controller.OnFrameworkUpdate;
        ClientState.Logout += controller.OnLogout;
        ClientState.TerritoryChanged += controller.OnTerritoryChanged;
    }

    public void Dispose()
    {
        Framework.Update -= controller.OnFrameworkUpdate;
        ClientState.Logout -= controller.OnLogout;
        ClientState.TerritoryChanged -= controller.OnTerritoryChanged;
        PluginInterface.UiBuilder.Draw -= windowSystem.Draw;
        PluginInterface.UiBuilder.OpenMainUi -= window.Toggle;
        PluginInterface.UiBuilder.OpenConfigUi -= window.Toggle;
        windowSystem.RemoveAllWindows();
        CommandManager.RemoveHandler(CommandName);

        // The CharaView must be released on the framework thread.
        Framework.RunOnFrameworkThread(controller.Dispose).Wait();
    }
}
