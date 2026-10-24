using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Windowing;

namespace GlamourBrowser.UI;

public sealed class MainWindow : Window
{
    public MainWindow()
        : base("GlamourBrowser##GlamourBrowser")
    {
        SizeConstraints = new WindowSizeConstraints { MinimumSize = new Vector2(900, 600) };
    }

    public override void Draw()
    {
        ImGui.Text("GlamourBrowser – im Aufbau");
    }
}
